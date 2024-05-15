using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Base class for command executors bound to a specific <see cref="DIContainerDefinition{TScopeData}"/>.
    /// Its responsibility is to expose a "Start" or "Submit" method that takes a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
    /// (and any other parameter), setup a <see cref="CrisJob"/> and call <see cref="CrisExecutionHost.StartJob(CrisJob)"/>.
    /// <para>
    /// It can optionally override the <see cref="ContainerCommandExecutor"/> virtual methods to communicate execution outcomes
    /// to the external world.
    /// </para>
    /// <para>
    /// The "Start" or "Submit" method may setup and return a <see cref="IExecutingCommand{T}"/> if needed: this is what the
    /// CrisBackgroundExecutorService does (in CK.Cris.BackgroundExecutor package).
    /// </para>
    /// </summary>
    /// <typeparam name="T">The scoped data type of the endpoint.</typeparam>
    [CKTypeDefiner]
    public abstract class ContainerCommandExecutor<T> : ContainerCommandExecutor, ISingletonAutoService where T : class, DIContainerDefinition.IScopedData
    {
        readonly CrisExecutionHost _executionHost;
        readonly IDIContainer<T> _container;

        protected ContainerCommandExecutor( CrisExecutionHost executionHost, IDIContainer<T> container )
        {
            Throw.CheckNotNullArgument( executionHost );
            Throw.CheckNotNullArgument( container );
            _executionHost = executionHost;
            _container = container;
        }

        /// <summary>
        /// Gets the execution host used by this executor.
        /// The same execution host can be used by multiple executors at the same time since 
        /// host's responsibility is only to manage the runners.
        /// </summary>
        public CrisExecutionHost ExecutionHost => _executionHost;

        /// <summary>
        /// Gets the container that executes the commands.
        /// </summary>
        public IDIContainer<T> DIContainer => _container;

        /// <summary>
        /// Default implementation creates a <see cref="AsyncServiceScope"/> from the <see cref="CrisJob.ScopedData"/> for the <see cref="DIContainer"/>.
        /// <para>
        /// This is enough for endpoint containers but background containers should override this to check for a null <see cref="DIContainerDefinition.BackendScopedData.AmbientServiceHub"/>
        /// to restore a new one from the command thanks to <see cref="RawCrisExecutor.RestoreAmbientServicesAsync(IActivityMonitor, ICrisPoco)"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="job">The starting job.</param>
        /// <returns>At this level, no error and the the configured DI scope to use.</returns>
        internal protected override ValueTask<(ICrisResultError?,AsyncServiceScope)> PrepareJobAsync( IActivityMonitor monitor, CrisJob job )
        {
            AsyncServiceScope s = _container.GetContainer().CreateAsyncScope( Unsafe.As<T>( job._scopedData ) );
            return ValueTask.FromResult<(ICrisResultError?, AsyncServiceScope)>( (null, s) );
        }
    }

}
