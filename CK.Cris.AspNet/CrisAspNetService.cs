using CK.Core;
using CK.Cris.AmbientValues;
using CK.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
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
    [AlsoRegisterType( typeof( CrisBackgroundExecutor ) )]
    [AlsoRegisterType( typeof( IAmbientValuesCollectCommand ) )]
    public partial class CrisAspNetService : ISingletonAutoService
    {
        readonly RawCrisValidator _validator;
        readonly CrisBackgroundExecutor _backgroundExecutor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<IAspNetCrisResult> _resultFactory;
        readonly IPocoFactory<IAspNetCrisResultError> _errorResultFactory;
        readonly IPocoFactory<ICrisResultError> _crisErrorResultFactory;

        public CrisAspNetService( PocoDirectory poco,
                                  RawCrisValidator validator,
                                  CrisBackgroundExecutor backgroundExecutor,
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

        internal async Task HandleRequestAsync( IActivityMonitor monitor,
                                                IServiceProvider requestServices,
                                                HttpRequest request,
                                                HttpResponse response,
                                                bool isNetPath )
        {
            // There is no try catch here and this is intended. An unhandled exception here
            // is an Internal Server Error that should bubble up.
            using( HandleIncomingCKDepToken( monitor, request, out var depToken ) )
            {
                // If we cannot read the command, it is considered as a Validation error.
                (IAbstractCommand? cmd, IAspNetCrisResult? result) = await ReadCommandAsync( monitor, request );
                if( result == null )
                {
                    Throw.DebugAssert( cmd != null );
                    //
                    EndpointUbiquitousInfo? info = HandleEndpointUbiquitousInfoConfigurator( requestServices, cmd );
                    if( info != null )
                    {
                        var c = _backgroundExecutor.Start( monitor, cmd, info, issuerToken: depToken );
                        var o = await c.SafeCompletion;
                        result = _resultFactory.Create();
                        result.Result = o;
                    }
                    else
                    {
                        result = await ValidateAndExecuteInlineAsync( monitor, requestServices, cmd );
                    }
                }
                if( result.CorrelationId == null )
                {
                    result.CorrelationId = monitor.CreateToken().ToString();
                }
                if( !isNetPath && result.Result is ICrisResultError error )
                {
                    IAspNetCrisResultError simpleError = _errorResultFactory.Create();
                    simpleError.IsValidationError = error.IsValidationError;
                    simpleError.LogKey = error.LogKey;
                    simpleError.Messages.AddRange( error.Messages.Select( m => m.AsSimpleUserMessage() ) );
                    result.Result = simpleError;
                }
                using( var writer = new Utf8JsonWriter( response.BodyWriter ) )
                {
                    PocoJsonSerializer.Write( result, writer, withType: false );
                }
                // A Cris result HTTP status code is always 200 OK except
                // on Internal Server Error.
                response.StatusCode = StatusCodes.Status200OK;
            }
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
                error.Messages.AddRange( validation.Messages );
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
                error.LogKey = CK.Cris.PocoFactoryExtensions.OnUnhandledError( monitor, currentCulture, true, ex, cmd, error.Messages );
                result.Result = error;
            }
            return result;
        }


        async Task<(IAbstractCommand?, IAspNetCrisResult?)> ReadCommandAsync( IActivityMonitor monitor, HttpRequest request )
        {
            int length = -1;
            using( var buffer = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream() )
            {
                try
                {
                    await request.Body.CopyToAsync( buffer );
                    length = (int)buffer.Position;
                    UserMessage? error;
                    if( length > 0 )
                    {
                        (var cmd, error) = ReadCommand( monitor, request.HttpContext.RequestServices, _poco, buffer.GetReadOnlySequence() );
                        if( cmd != null )
                        {
                            Throw.DebugAssert( error == null );
                            return (cmd, null);
                        }
                        Throw.DebugAssert( error != null );
                    }
                    else
                    {
                        error = UserMessage.Error( request.HttpContext.RequestServices.GetRequiredService<CurrentCultureInfo>(),
                                                   "Unable to read Command Poco from empty request body.",
                                                   "Cris.AspNet.EmptyBody" );
                    }
                    return (null, CreateValidationErrorResult( error.Value, null ));
                }
                catch( Exception ex )
                {
                    UserMessage errorMessage = UserMessage.Error( request.HttpContext.RequestServices.GetRequiredService<CurrentCultureInfo>(),
                                                                  $"Unable to read Command Poco from request body (byte length = {length}).",
                                                                  "Cris.AspNet.ReadCommandFailed" );
                    using var gError = monitor.OpenError( errorMessage.Message.CodeString, ex );
                    var x = ReadBodyTextOnError( buffer.GetReadOnlySequence() );
                    if( x.B != null )
                    {
                        monitor.Trace( x.B );
                    }
                    else
                    {
                        monitor.Error( "Error while tracing request body.", x.E );
                    }
                    var error = CreateValidationErrorResult( errorMessage, gError.GetLogKeyString() );
                    return (null, error);
                }
            }

            static (IAbstractCommand?, UserMessage?) ReadCommand( IActivityMonitor monitor, IServiceProvider services, PocoDirectory p, in ReadOnlySequence<byte> buffer )
            {
                var reader = new Utf8JsonReader( buffer );
                var poco = p.Read( ref reader );
                if( poco == null ) return (null, UserMessage.Error( services.GetRequiredService<CurrentCultureInfo>(),
                                                                    $"Received a null Poco.",
                                                                    "Cris.AspNet.ReceiveNullPoco" ));
                if( poco is not IAbstractCommand c )
                {
                    return (null, UserMessage.Error( services.GetRequiredService<CurrentCultureInfo>(),
                                                     $"Received Poco is not a Command but a '{((IPocoGeneratedClass)poco).Factory.Name}'.",
                                                     "Cris.AspNet.NotACommand" ) );
                }
                return (c,null);
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

        IAspNetCrisResult CreateValidationErrorResult( UserMessage message, string? logKey )
        {
            IAspNetCrisResult result = _resultFactory.Create();
            ICrisResultError e = _crisErrorResultFactory.Create( message );
            e.IsValidationError = true;
            e.LogKey = logKey;
            result.Result = e;
            return result;
        }

    }
}
