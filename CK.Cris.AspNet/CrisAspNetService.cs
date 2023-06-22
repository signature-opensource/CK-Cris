using CK.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    [EndpointSingletonService]
    public partial class CrisAspNetService : ISingletonAutoService
    {
        readonly RawCrisValidator _validator;
        readonly RawCrisExecutor _executor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<ICrisResult> _resultFactory;
        readonly IPocoFactory<ICrisResultError> _errorResultFactory;

        public CrisAspNetService( PocoDirectory poco,
                                  RawCrisValidator validator,
                                  RawCrisExecutor executor,
                                  IPocoFactory<ICrisResult> resultFactory,
                                  IPocoFactory<ICrisResultError> errorResultFactory )
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
                    Debug.Assert( cmd != null );
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
            // The validation return null if no issues occurred, a code V for validation errors and a Code E if an exception occurred
            // (Command validators must not raise any exception).
            // We use the request monitor to collect the validation results: warnings and errors will appear in the logs.
            ICrisResult? result = await GetValidationErrorAsync( monitor, requestServices, cmd );
            if( result == null )
            {
                // Valid command: calls the execution handler.
                Debug.Assert( cmd != null );
                try
                {
                    var execContext = requestServices.GetRequiredService<CrisExecutionContext>();
                    var (o, _) = await execContext.ExecuteAsync( cmd );
                    return _resultFactory.Create( r => { r.Code = VESACode.Synchronous; r.Result = o; } );
                }
                catch( Exception ex )
                {
                    result = _resultFactory.Create();
                    result.Code = VESACode.Error;
                    try
                    {
                        var errorHandler = requestServices.GetService<IFrontCommandExceptionHandler>();
                        if( errorHandler == null )
                        {
                            monitor.Error( $"Error while executing {cmd.GetType():C} occurred (no IFrontCommandExceptionHandler service available in the Services).", ex );
                            result.Result = _errorResultFactory.Create( ex.Message );
                        }
                        else
                        {
                            await errorHandler.OnErrorAsync( monitor, requestServices, ex, cmd, result );
                            if( result.Result == null || (result.Result is IEnumerable e && !e.GetEnumerator().MoveNext()) )
                            {
                                var msg = $"IFrontCommandExceptionHandler '{errorHandler.GetType().Name}' failed to add any error result. The exception message is added.";
                                monitor.Error( msg );
                                result.Result = _errorResultFactory.Create( msg, ex.Message );
                            }
                        }
                    }
                    catch( Exception ex2 )
                    {
                        using( monitor.OpenFatal( "Error in ErrorHandler.", ex2 ) )
                        {
                            monitor.Error( "Original error.", ex );
                        }
                        result.Result = ex2.Message;
                    }
                }
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
                    string? error;
                    if( length > 0 )
                    {
                        (var cmd, error) = ReadCommand( monitor, _poco, buffer.GetReadOnlySequence() );
                        if( cmd != null )
                        {
                            Debug.Assert( error == null );
                            return (cmd, null);
                        }
                        Debug.Assert( error != null );
                    }
                    else
                    {
                        error = "Unable to read Command Poco from empty request body.";
                    }
                    return (null, CreateExceptionResult( error, null, VESACode.ValidationError ));
                }
                catch( Exception ex )
                {
                    string errorMessage;
                    if( length < 0 )
                    {
                        errorMessage = $"Unable to read Command Poco from request body.";
                        monitor.Error( errorMessage, ex );
                    }
                    else
                    {
                        errorMessage = $"Unable to read Command Poco from request body (byte length = {length}).";
                        var x = ReadBodyTextOnError( buffer.GetReadOnlySequence() );
                        if( x.B != null )
                        {
                            monitor.Error( errorMessage + " Body: " + x.B, ex );
                        }
                        else
                        {
                            using( monitor.OpenError( errorMessage, ex ) )
                            {
                                monitor.Error( "While tracing request body.", x.E );
                            }
                        }
                    }
                    return (null, CreateExceptionResult( errorMessage, ex, VESACode.ValidationError ));
                }
            }

            static (IAbstractCommand?, string?) ReadCommand( IActivityMonitor monitor, PocoDirectory p, in ReadOnlySequence<byte> buffer )
            {
                var reader = new Utf8JsonReader( buffer );
                var poco = p.Read( ref reader );
                if( poco == null ) return (null, "Received a null Poco." );
                if( poco is not IAbstractCommand c )
                {
                    return (null, $"Received Poco is not a Command but a '{((IPocoGeneratedClass)poco).Factory.Name}'.");
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

        async Task<ICrisResult?> GetValidationErrorAsync( IActivityMonitor monitor, IServiceProvider requestServices, IAbstractCommand cmd )
        {
            try
            {
                CrisValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
                if( !validation.Success )
                {
                    ICrisResult result = _resultFactory.Create();
                    result.Code = VESACode.ValidationError;
                    result.Result = CreateErrorValidationResult( validation );
                    return result;
                }
            }
            catch( Exception ex )
            {
                return CreateExceptionResult( "CommandValidator unexpected error.", ex, VESACode.Error );
            }
            return null;
        }

        ICrisResultError CreateErrorValidationResult( CrisValidationResult v )
        {
            var r = _errorResultFactory.Create();
            r.Errors.AddRange( v.Errors );
            return r;
        }

        ICrisResult CreateExceptionResult( string message, Exception? ex, VESACode code )
        {
            ICrisResult result = _resultFactory.Create();
            result.Code = code;
            result.Result = _errorResultFactory.Create( message, ex?.Message );
            return result;
        }

    }
}
