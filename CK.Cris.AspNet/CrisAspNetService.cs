using CK.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    public class CrisAspNetService : ISingletonAutoService
    {
        readonly CommandValidator _validator;
        readonly CommandExecutor _executor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<ICrisResult> _resultFactory;

        public CrisAspNetService( PocoDirectory poco, CommandValidator validator, CommandExecutor executor, IPocoFactory<ICrisResult> resultFactory )
        {
            _poco = poco;
            _validator = validator;
            _executor = executor;
            _resultFactory = resultFactory;
        }

        IDisposable? HandleIncomingCKDepToken( IActivityMonitor monitor, HttpRequest request )
        {
            // This handles the first valid token if multiple tokens are provided (and StringValues enumerator is fast).
            // Multiple tokens makes no real sense.
            foreach( var t in request.Headers["CKDepToken"] )
            {
                if( ActivityMonitor.DependentToken.TryParse( t, out var token ) )
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
                (ICommand? cmd, ICrisResult? result) = await ReadCommandAsync( monitor, request );
                if( result == null )
                {
                    Debug.Assert( cmd != null );
                    // The validation return null if no issues occurred, a code V for validation errors and a Code E if an exception occurred
                    // (Command validators must not raise any exception).
                    result = await GetValidationErrorAsync( monitor, requestServices, cmd );
                }
                if( result == null )
                {
                    // Valid command: calls the execution handler.
                    Debug.Assert( cmd != null );
                    result = await _executor.ExecuteCommandAsync( monitor, requestServices, cmd );
                }
                if( result.CorrelationId == null )
                {
                    result.CorrelationId = monitor.CreateDependentToken().ToString();
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

        async Task<(ICommand?, ICrisResult?)> ReadCommandAsync( IActivityMonitor monitor, HttpRequest request )
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

            static (ICommand?, string?) ReadCommand( IActivityMonitor monitor, PocoDirectory p, in ReadOnlySequence<byte> buffer )
            {
                var reader = new Utf8JsonReader( buffer );
                var poco = p.Read( ref reader );
                if( poco == null ) return (null, "Received a null Poco." );
                if( poco is not ICommand c )
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

        async Task<ICrisResult?> GetValidationErrorAsync( IActivityMonitor monitor, IServiceProvider requestServices, ICommand cmd )
        {
            try
            {
                ValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
                if( !validation.Success )
                {
                    ICrisResult result = _resultFactory.Create();
                    result.Code = VESACode.ValidationError;
                    result.Result = _validator.CreateSimpleErrorResult( validation );
                    return result;
                }
            }
            catch( Exception ex )
            {
                return CreateExceptionResult( "CommandValidator unexpected error.", ex, VESACode.Error );
            }
            return null;
        }

        ICrisResult CreateExceptionResult( string message, Exception? ex, VESACode code )
        {
            ICrisResult result = _resultFactory.Create();
            result.Code = code;
            result.Result = _executor.CreateErrorResult( message, ex?.Message );
            return result;
        }

    }
}
