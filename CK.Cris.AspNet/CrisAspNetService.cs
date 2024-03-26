using CK.Core;
using CK.Cris.AmbientValues;
using CK.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    [EndpointSingletonService]
    [AlsoRegisterType( typeof( CrisDirectory ) )]
    [AlsoRegisterType( typeof( TypeScriptCrisCommandGenerator ) )]
    [AlsoRegisterType( typeof( PocoJsonSerializer ) )]
    [AlsoRegisterType( typeof( RawCrisValidator ) )]
    [AlsoRegisterType( typeof( IAspNetCrisResult ) )]
    [AlsoRegisterType( typeof( IAspNetCrisResultError ) )]
    [AlsoRegisterType( typeof( CrisBackgroundExecutorService ) )]
    [AlsoRegisterType( typeof( IAmbientValuesCollectCommand ) )]
    public partial class CrisAspNetService : ISingletonAutoService
    {
        readonly RawCrisValidator _validator;
        readonly CrisBackgroundExecutorService _backgroundExecutor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<IAspNetCrisResult> _resultFactory;
        readonly IPocoFactory<IAspNetCrisResultError> _errorResultFactory;
        readonly IPocoFactory<ICrisResultError> _crisErrorResultFactory;

        public CrisAspNetService( PocoDirectory poco,
                                  RawCrisValidator validator,
                                  CrisBackgroundExecutorService backgroundExecutor,
                                  IPocoFactory<IAspNetCrisResult> resultFactory,
                                  IPocoFactory<IAspNetCrisResultError> errorResultFactory,
                                  IPocoFactory<ICrisResultError> backendErrorResultFactory )
        {
            _poco = poco;
            _validator = validator;
            _backgroundExecutor = backgroundExecutor;
            _resultFactory = resultFactory;
            _errorResultFactory = errorResultFactory;
            _crisErrorResultFactory = backendErrorResultFactory;
        }

        /// <summary>
        /// Handles a command request parsed with the provided <paramref name="reader"/>: the command is
        /// validated and executed by the <see cref="CrisBackgroundExecutorService"/> or inline if it can.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="request">The http request.</param>
        /// <param name="reader">The payload reader.</param>
        /// <param name="useSimpleError">False to keep <see cref="ICrisResultError"/> instead of the simpler <see cref="IAspNetCrisResultError"/>.</param>
        /// <param name="currentCultureInfo">Optional current culture.</param>
        /// <returns>The command result.</returns>
        public async Task<IAspNetCrisResult> HandleRequestAsync( IActivityMonitor monitor,
                                                                 HttpRequest request,
                                                                 CommandRequestReader reader,
                                                                 bool useSimpleError = true,
                                                                 CurrentCultureInfo? currentCultureInfo = null )
        {
            // There is no try catch here and this is intended. An unhandled exception here
            // is an Internal Server Error that should bubble up.
            var requestServices = request.HttpContext.RequestServices;
            using( HandleIncomingCKDepToken( monitor, request, out var depToken ) )
            {
                // If we cannot read the command, it is considered as a Validation error.
                (IAbstractCommand? cmd, IAspNetCrisResult? result) = await ReadCommandAsync( monitor, request, reader, currentCultureInfo );
                if( result == null )
                {
                    Throw.DebugAssert( cmd != null );
                    //
                    EndpointUbiquitousInfo? info = HandleEndpointUbiquitousInfoConfigurator( requestServices, cmd );
                    if( info != null )
                    {
                        var c = _backgroundExecutor.Submit( monitor, cmd, info, issuerToken: depToken );
                        var o = await c.SafeCompletion;
                        result = _resultFactory.Create();
                        result.Result = o;
                    }
                    else
                    {
                        result = await ValidateAndExecuteInlineAsync( monitor, requestServices, cmd );
                    }
                }
                result.CorrelationId ??= monitor.CreateToken().ToString();
                if( !useSimpleError && result.Result is ICrisResultError error )
                {
                    IAspNetCrisResultError simpleError = _errorResultFactory.Create();
                    // On validation errors, the IAspNetCrisResult.ValidationMessages contains all the messages.
                    // The simplified IAspNetCrisResultError always contains
                    // only the errors as string.
                    simpleError.Errors.AddRange( error.Errors.Where( e => e.Level == UserMessageLevel.Error ).Select( m => m.Text ) );
                    if( error.IsValidationError )
                    {
                        result.ValidationMessages = error.Errors.Select( m => m.AsSimpleUserMessage() ).ToList();
                        simpleError.IsValidationError = true;
                    }
                    simpleError.LogKey = error.LogKey;
                    result.Result = simpleError;
                }
                /// A Cris result HTTP status code must always be 200 OK (except on Internal Server Error).
                request.HttpContext.Response.StatusCode = 200;
                return result;
            }
        }


        static IDisposable? HandleIncomingCKDepToken( IActivityMonitor monitor, HttpRequest request, out ActivityMonitor.Token? token )
        {
            // This handles the first valid token if multiple tokens are provided (and StringValues enumerator is fast).
            // Multiple tokens makes no real sense.
            foreach( var t in request.Headers["CKDepToken"] )
            {
                if( ActivityMonitor.Token.TryParse( t, out token ) )
                {
                    return monitor.StartDependentActivity( token );
                }
                monitor.Warn( $"Invalid request CKDepToken header value: '{t}'. Ignored." );
            }
            token = null;
            return null;
        }

        // Temporary:
        // TODO: Handle this "context matching" in a generic way ([ConfigureEndpointServices] attribute).
        EndpointUbiquitousInfo? HandleEndpointUbiquitousInfoConfigurator( IServiceProvider requestServices, IAbstractCommand? cmd )
        {
            EndpointUbiquitousInfo? info = null;
            if( cmd is ICommandWithCurrentCulture c )
            {
                info = requestServices.GetRequiredService<EndpointUbiquitousInfo>();
                CrisCultureService.ConfigureCurrentCulture( c, info );
                if( !info.IsDirty ) info = null;
            }
            return info;
        }

        /// <summary>
        /// Validates and executes the command in the context of the end point.
        /// </summary>
        async Task<IAspNetCrisResult> ValidateAndExecuteInlineAsync( IActivityMonitor monitor,
                                                                     IServiceProvider requestServices,
                                                                     IAbstractCommand cmd )
        {
            IAspNetCrisResult result = _resultFactory.Create();
            CrisValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
            if( !validation.Success )
            {
                ICrisResultError error = _crisErrorResultFactory.Create();
                error.Errors.AddRange( validation.Messages );
                error.LogKey = validation.LogKey;
                error.IsValidationError = true;
                result.Result = error;
                return result;
            }
            // Valid command: calls the execution handler.
            Throw.DebugAssert( cmd != null );
            try
            {
                var execContext = requestServices.GetRequiredService<CrisExecutionContext>();
                var (o, _) = await execContext.ExecuteAsync( cmd );
                result.Result = o;
            }
            catch( Exception ex )
            {
                var currentCulture = requestServices.GetRequiredService<CurrentCultureInfo>();
                ICrisResultError error = _crisErrorResultFactory.Create();
                error.LogKey = PocoFactoryExtensions.OnUnhandledError( monitor, ex, cmd, true, currentCulture, error.Errors.Add );
                result.Result = error;
            }
            return result;
        }


        async Task<ReadCommandResult> ReadCommandAsync( IActivityMonitor monitor, HttpRequest request, CommandRequestReader reader, CurrentCultureInfo? currentCultureInfo )
        {
            int length = -1;
            using( var buffer = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream() )
            {
                currentCultureInfo ??= request.HttpContext.RequestServices.GetRequiredService<CurrentCultureInfo>();
                var messageCollector = new UserMessageCollector( currentCultureInfo );
                try
                {
                    await request.Body.CopyToAsync( buffer );
                    length = (int)buffer.Position;
                    if( length > 0 )
                    {
                        var cmd = await reader( monitor, request, _poco, messageCollector, buffer.GetReadOnlySequence() );
                        if( cmd != null )
                        {
                            if( messageCollector.ErrorCount > 0 )
                            {
                                using( monitor.OpenWarn( $"Command '{cmd}' has been successfuly read but {messageCollector.ErrorCount} have been emitted." ) )
                                {
                                    foreach( var e in messageCollector.UserMessages.Where( m => m.Level == UserMessageLevel.Error ) )
                                    {
                                        monitor.Warn( e.Text );
                                    }
                                }
                            }
                            return new ReadCommandResult( cmd );
                        }
                        else
                        {
                            if( messageCollector.ErrorCount == 0 )
                            {
                                monitor.Error( ActivityMonitor.Tags.ToBeInvestigated, "The command reader returned null but no error has been emitted." );
                                messageCollector.Error( "Request failed to be read without explicit error.", "Cris.AspNet.ReadNullCommandMissingError" );
                            }
                        }
                    }
                    else
                    {
                        messageCollector.Error( "Unable to read Command Poco from empty request body.", "Cris.AspNet.EmptyBody" );
                    }
                    return new ReadCommandResult( CreateValidationErrorResult( messageCollector, null ) );
                }
                catch( Exception ex )
                {
                    messageCollector.Error( $"Unable to read Command Poco from request body (byte length = {length}).", "Cris.AspNet.ReadCommandFailed" );
                    using var gError = monitor.OpenError( messageCollector.UserMessages[^1].Message.CodeString, ex );
                    var (body, error) = ReadBodyTextOnError( buffer.GetReadOnlySequence() );
                    if( body != null )
                    {
                        monitor.Trace( body );
                    }
                    else
                    {
                        monitor.Error( "Error while tracing request body.", error );
                    }
                    return new ReadCommandResult( CreateValidationErrorResult( messageCollector, gError.GetLogKeyString() ) );
                }
            }

            static (string? B, Exception? E) ReadBodyTextOnError( in ReadOnlySequence<byte> buffer )
            {
                try
                {
                    return (Encoding.UTF8.GetString( buffer ), null );
                }
                catch( Exception ex )
                {
                    return (null,ex);
                }

            }

        }

        /// <summary>
        /// Standard <see cref="CommandRequestReader"/> of a paylaod that is a <see cref="IAbstractCommand"/> poco in JSON format.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="request">The http request.</param>
        /// <param name="pocoDirectory">The poco directory.</param>
        /// <param name="messageCollector">The message collector to use for errors, warnings and logs.</param>
        /// <param name="payload">The request payload.</param>
        /// <returns>
        /// A non null command on success. When null, at least one <see cref="UserMessageLevel.Error"/> message should be in the collector.
        /// </returns>
        public static ValueTask<IAbstractCommand?> StandardReadCommandAsync( IActivityMonitor monitor,
                                                                             HttpRequest request,
                                                                             PocoDirectory pocoDirectory,
                                                                             UserMessageCollector messageCollector,
                                                                             ReadOnlySequence<byte> payload )
        {
            var reader = new Utf8JsonReader( payload );
            var poco = pocoDirectory.Read( ref reader );
            if( poco == null )
            {
                messageCollector.Error( "Received a null Poco.", "Cris.AspNet.ReceiveNullPoco" );
                return ValueTask.FromResult<IAbstractCommand?>( null );
            }
            if( poco is not IAbstractCommand c )
            {
                messageCollector.Error( $"Received Poco is not a Command but a '{((IPocoGeneratedClass)poco).Factory.Name}'.", "Cris.AspNet.NotACommand" );
                return ValueTask.FromResult<IAbstractCommand?>( null );
            }
            return ValueTask.FromResult<IAbstractCommand?>( c );
        }


        IAspNetCrisResult CreateValidationErrorResult( UserMessageCollector messages, string? logKey )
        {
            IAspNetCrisResult result = _resultFactory.Create();
            ICrisResultError e = _crisErrorResultFactory.Create();

            var validationMessages = new List<SimpleUserMessage>();
            foreach( var message in messages.UserMessages )
            {
                if( message.Level == UserMessageLevel.Error ) e.Errors.Add( message );
                validationMessages.Add( message );
            }
            result.ValidationMessages = validationMessages;

            e.IsValidationError = true;
            e.LogKey = logKey;
            result.Result = e;
            return result;
        }

    }
}
