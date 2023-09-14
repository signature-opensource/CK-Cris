using CK.Core;
using CK.Setup;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    [EndpointSingletonService]
    [AlsoRegisterType( typeof( CrisDirectory ) )]
    [AlsoRegisterType( typeof( ISimpleCrisResultError ) )]
    [AlsoRegisterType( typeof( RawCrisValidator ) )]
    [AlsoRegisterType( typeof( RawCrisExecutor ) )]
    public partial class CrisAspNetService : ISingletonAutoService
    {
        readonly RawCrisValidator _validator;
        readonly RawCrisExecutor _executor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<ICrisResult> _resultFactory;
        readonly IPocoFactory<ISimpleCrisResultError> _errorResultFactory;

        public CrisAspNetService( PocoDirectory poco,
                                  RawCrisValidator validator,
                                  RawCrisExecutor executor,
                                  IPocoFactory<ICrisResult> resultFactory,
                                  IPocoFactory<ISimpleCrisResultError> errorResultFactory )
        {
            _poco = poco;
            _validator = validator;
            _executor = executor;
            _resultFactory = resultFactory;
            _errorResultFactory = errorResultFactory;
        }

        IDisposable? HandleIncomingCKDepToken( IActivityMonitor monitor, HttpRequest request )
        {
            // This handles the first valid token if multiple tokens are provided (and StringValues enumerator is fast).
            // Multiple tokens makes no real sense.
            foreach( var t in request.Headers["CKDepToken"] )
            {
                if( ActivityMonitor.Token.TryParse( t, out var token ) )
                {
                    return monitor.StartDependentActivity( token );
                }
                monitor.Warn( $"Invalid request CKDepToken header value: '{t}'. Ignored." );
            }
            return null;
        }

        public async Task HandleRequestAsync( IActivityMonitor monitor, IServiceProvider requestServices, HttpRequest request, HttpResponse response )
        {
            // There is no try catch here and this is intended. An unhandled exception here
            // is an Internal Server Error that should bubble up.
            using( HandleIncomingCKDepToken( monitor, request ) )
            {
                // If we cannot read the command, it is considered as a Code V (Validation error), hence a BadRequest status code.
                (IAbstractCommand? cmd, ICrisResult? result) = await ReadCommandAsync( monitor, request );
                if( result == null )
                {
                    Throw.DebugAssert( cmd != null );
                    result = await ValidateAndExecuteAsync( monitor, requestServices, cmd );
                }
                if( result.CorrelationId == null )
                {
                    result.CorrelationId = monitor.CreateToken().ToString();
                }
                using( var writer = new Utf8JsonWriter( response.BodyWriter ) )
                {
                    PocoJsonSerializer.Write( result, writer );
                }
                // A Cris result HTTP status code is always 200 OK except
                // on Internal Server Error.
                response.StatusCode = StatusCodes.Status200OK;
            }
        }

        async Task<ICrisResult> ValidateAndExecuteAsync( IActivityMonitor monitor, IServiceProvider requestServices, IAbstractCommand cmd )
        {
            ICrisResult result = _resultFactory.Create();
            result.Code = VESACode.ValidationError;
            CrisValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
            if( !validation.Success )
            {
                var error = _errorResultFactory.Create();
                error.Messages.AddRange( validation.Messages.Select( m => (m.Level, m.Message.Text, m.Depth) ) );
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
                result.Code = VESACode.Synchronous;
                result.Result = o;
            }
            catch( Exception ex )
            {
                result.Code = VESACode.Error;
                var currentCulture = requestServices.GetRequiredService<CurrentCultureInfo>();
                var error = _errorResultFactory.Create();
                error.LogKey = CK.Cris.PocoFactoryExtensions.OnUnhandledError( monitor, currentCulture, true, ex, cmd, out var genericError );
                if( ex is MCException mc )
                {
                    error.Messages.Add( (UserMessageLevel.Error, mc.Message, 0) );
                }
                error.Messages.Add( (UserMessageLevel.Error, genericError.Message, 1 ) );
                result.Result = error;
            }
            return result;
        }


        async Task<(IAbstractCommand?, ICrisResult?)> ReadCommandAsync( IActivityMonitor monitor, HttpRequest request )
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
                    return (null, CreateErrorResult( error.Value, VESACode.ValidationError, null ));
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
                    var error = CreateErrorResult( errorMessage, VESACode.ValidationError, gError.GetLogKeyString() );
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

        ICrisResult CreateErrorResult( UserMessage message, VESACode code, string? logKey )
        {
            ICrisResult result = _resultFactory.Create();
            result.Code = code;
            ISimpleCrisResultError e = _errorResultFactory.Create( message );
            e.LogKey = logKey;
            result.Result = e;
            return result;
        }

    }
}
