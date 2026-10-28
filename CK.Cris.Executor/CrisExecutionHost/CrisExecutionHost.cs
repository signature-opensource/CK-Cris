using CK.Core;
using CK.PerfectEvent;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Immutable;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// A Cris execution host handles <see cref="CrisJob"/> (submitted by <see cref="ContainerCommandExecutor"/>)
/// in the background thanks to a variable count of parallel runners.
/// <para>
/// This is a <see cref="ISingletonAutoService"/>: the default instance is available in all the DI containers
/// (but nothing prevents other host to be instantiated and used independently).
/// </para>
/// </summary>
[AlsoRegisterType<RawCrisReceiver, RawCrisExecutor, DarkSideCrisEventHub>]
public sealed partial class CrisExecutionHost : ICrisExecutionHost, ISingletonAutoService
{
    readonly IPocoFactory<ICrisResultError> _errorResultFactory;
    readonly DarkSideCrisEventHub _eventHub;
    readonly RawCrisReceiver _crisReceiver;
    readonly RawCrisExecutor _rawExecutor;
    readonly ICrisAmbientScopeFactory? _scopeFactory;

    readonly PerfectEventSender<ICrisExecutionHost> _parallelRunnerCountChanged;
    // We use null as the close signal for runners and push int values to regulate the count of
    // runners.
    // The _channel is used as the lock to manage runners.
    // This is a multi-writers/multi-readers channel: background execution
    // can be requested concurrently and multiple parallel runners run.
    readonly Channel<object?> _channel;
    Runner? _last;
    int _runnerCount;
    int _plannedRunnerCount;
    // Ever increasing number.
    int _runnerNumber;

    /// <summary>
    /// Initializes a new <see cref="CrisExecutionHost"/> with a single initial runner.
    /// <para>
    /// The <paramref name="validator"/> is used only when <see cref="CrisJob.IncomingValidationCheck"/> is true.
    /// </para>
    /// </summary>
    /// <param name="eventHub">The cris event hub.</param>
    /// <param name="validator">The command validator.</param>
    /// <param name="executor">The command executor.</param>
    /// <param name="scopeFactory">
    /// Optional factory of scopes with other ambient services (CK.Cris.BackgroundExecutor): when the incoming validation
    /// of a job configures ambient services, the command executes inline in such a scope.
    /// </param>
    public CrisExecutionHost( DarkSideCrisEventHub eventHub,
                              RawCrisReceiver validator,
                              RawCrisExecutor executor,
                              ICrisAmbientScopeFactory? scopeFactory = null )
    {
        Throw.CheckNotNullArgument( eventHub );
        Throw.CheckNotNullArgument( validator );
        Throw.CheckNotNullArgument( executor );
        _errorResultFactory = eventHub.PocoDirectory.Find<ICrisResultError>()!;
        _eventHub = eventHub;
        _crisReceiver = validator;
        _rawExecutor = executor;
        _scopeFactory = scopeFactory;
        _channel = Channel.CreateUnbounded<object?>();
        _parallelRunnerCountChanged = new PerfectEventSender<ICrisExecutionHost>();
        _plannedRunnerCount = 1;
        _runnerCount = 1;
        _last = new Runner( this, 0, null );
    }

    /// <inheritdoc />
    public int ParallelRunnerCount
    {
        get => _runnerCount;
        set
        {
            Throw.CheckOutOfRangeArgument( value >= 1 && value <= 1000 );
            Push( value );
        }
    }

    /// <inheritdoc />
    public PerfectEvent<ICrisExecutionHost> ParallelRunnerCountChanged => _parallelRunnerCountChanged.PerfectEvent;

    /// <summary>
    /// Starts a <see cref="CrisJob"/> execution: the job is sent to one available runner
    /// and will be executed in the background.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    public void StartJob( CrisJob job )
    {
        Throw.CheckNotNullArgument( job );
        Push( job );
    }

    /// <summary>
    /// Gets the Cris executor.
    /// </summary>
    public RawCrisExecutor RawCrisExecutor => _rawExecutor;

    void Push( object job ) => _channel.Writer.TryWrite( job );

    // When the incoming validation configured ambient services, the root command executes inline in a scope built
    // from them (with a view of the root context: the job's immediate events forwarding is kept). Without scope
    // factory, they cannot be honored: the command executes with the job's ambient services and this is logged.
    async Task<ExecutedCommand> ExecuteRootAsync( IActivityMonitor monitor,
                                                  CrisJob job,
                                                  CrisExecutionContext rootContext,
                                                  ImmutableArray<UserMessage> validationMessages,
                                                  AmbientServiceHub? configuredHub )
    {
        if( configuredHub != null )
        {
            if( _scopeFactory != null )
            {
                CrisExecutionContext? view = null;
                await using var inline = _scopeFactory.CreateScope( configuredHub, monitor, sp => view = rootContext.CreateView( sp ) );
                Throw.DebugAssert( view != null );
                return await view.ExecuteRootCommandAsync( job.Command, job.DeferredExecutionContext, validationMessages );
            }
            monitor.Warn( $"The incoming validation of '{job.Command.CrisPocoModel.PocoName}' configured ambient services but no {nameof( ICrisAmbientScopeFactory )} is available (CK.Cris.BackgroundExecutor): they are ignored." );
        }
        return await rootContext.ExecuteRootCommandAsync( job.Command, job.DeferredExecutionContext, validationMessages );
    }

    ValueTask ExecuteTypedJobAsync( IActivityMonitor monitor, object o )
    {
        if( o is CrisJob job ) return HandleCommandAsync( monitor, job );
        return HandleSetRunnerCountAsync( monitor, (int)o );
    }

    async ValueTask HandleCommandAsync( IActivityMonitor monitor, CrisJob job )
    {
        using var gLog = monitor.StartDependentActivityGroup( job.IssuerToken );

        ICrisResultError? error = null;
        AsyncServiceScope scoped = default;
        bool isScopedCreated = false;

        ImmutableArray<UserMessage> validationMessages = ImmutableArray<UserMessage>.Empty;
        ExecutedCommand executed;
        try
        {
            (error, scoped) = await job._executor.PrepareJobAsync( monitor, job );
            if( error != null )
            {
                // Here we should ensure that the AsyncServiceScope has a null ServiceProvider.
                //  Throw.CheckState( "AsyncServiceScope has not been obtained.", scoped is default );
                executed = job.CreateExecutedCommand( error, validationMessages, ImmutableArray<IEvent>.Empty );
                await job.SetFinalResultAsync( monitor, executed, null );
                return;
            }
            isScopedCreated = true;
            // Configure the data for the DI container: the monitor
            // is the one of the calling runner and the ExecutionContext
            // is bound to the new scoped service.
            job._runnerMonitor = monitor;
            var rootContext = new CrisJob.JobExecutionContext( job, monitor, scoped.ServiceProvider, _eventHub, _rawExecutor );
            // This ExecutionContext is now available in the DI container. Work can start.
            job._executionContext = rootContext;
            // The ambient services of the reception (for instance the identity derived from the sending party), before
            // the incoming validation: the validation and the execution then run in a scope built from them.
            (error, var receptionHub) = await job._executor.ConfigureReceptionAsync( monitor, job, scoped.ServiceProvider );
            if( error != null )
            {
                executed = job.CreateExecutedCommand( error, validationMessages, ImmutableArray<IEvent>.Empty );
                await job.SetFinalResultAsync( monitor, executed, scoped.ServiceProvider );
                return;
            }
            CrisExecutionContext context = rootContext;
            AsyncServiceScope reception = default;
            if( receptionHub != null )
            {
                if( _scopeFactory == null )
                {
                    // Failing closed: the command must not execute without the ambient services of its reception.
                    Throw.InvalidOperationException( $"The reception of '{job.Command.CrisPocoModel.PocoName}' configured ambient services but no {nameof( ICrisAmbientScopeFactory )} is available (CK.Cris.BackgroundExecutor)." );
                }
                CrisExecutionContext? view = null;
                reception = _scopeFactory.CreateScope( receptionHub, monitor, sp => view = rootContext.CreateView( sp ) );
                Throw.DebugAssert( view != null );
                context = view;
            }
            try
            {
                // The ambient services configured by the incoming validation ([ConfigureAmbientServices]), if any.
                AmbientServiceHub? configuredHub = null;
                if( job._incomingValidationCheck )
                {
                    var validation = await _crisReceiver.IncomingValidateAsync( monitor, context.ServiceProvider, job.Command, gLog );
                    if( !validation.Success )
                    {
                        error = _errorResultFactory.Create();
                        error.Errors.AddRange( validation.ErrorMessages );
                        error.LogKey = validation.LogKey;
                        error.IsValidationError = true;
                        executed = job.CreateExecutedCommand( error, validation.ValidationMessages, ImmutableArray<IEvent>.Empty );
                        await job.SetFinalResultAsync( monitor, executed, scoped.ServiceProvider );
                        return;
                    }
                    validationMessages = validation.ValidationMessages;
                    configuredHub = validation.AmbientServiceHub;
                }
                // Executing the command (handling validators, handler and post handlers).
                executed = await ExecuteRootAsync( monitor, job, context, validationMessages, configuredHub );
            }
            finally
            {
                if( receptionHub != null ) await reception.DisposeAsync();
            }
            await job.SetFinalResultAsync( monitor, executed, scoped.ServiceProvider );
        }
        catch( Exception ex )
        {
            // Ensures that the crisResult exists and sets its Result to a ICrisErrorResult.
            IServiceProvider? serviceProvider = isScopedCreated ? scoped.ServiceProvider : null;
            error = _errorResultFactory.Create();
            PocoFactoryExtensions.OnUnhandledError( monitor, ex, job.Command, true, serviceProvider?.GetService<CurrentCultureInfo>(), error.Errors.Add );
            error.LogKey = gLog.GetLogKeyString();

            executed = job.CreateExecutedCommand( error, validationMessages, ImmutableArray<IEvent>.Empty );
            await job.SetFinalResultAsync( monitor, executed, serviceProvider );
        }
        finally
        {
            if( isScopedCreated ) await scoped.DisposeAsync();
        }
    }

}
