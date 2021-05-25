using CK.Core;
using Microsoft.AspNetCore.Http;
using System;
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
        readonly IPocoFactory<ICommandResult> _resultFactory;

        public CrisAspNetService( PocoDirectory poco, CommandValidator validator, CommandExecutor executor, IPocoFactory<ICommandResult> resultFactory )
        {
            _poco = poco;
            _validator = validator;
            _executor = executor;
            _resultFactory = resultFactory;
        }

        public async Task HandleRequest( IActivityMonitor monitor, IServiceProvider requestServices, HttpRequest request, HttpResponse response )
        {
            // If we cannot read the command, it is considered as a Code V (Validation error), hence a BadRequest status code.
            (ICommand? cmd, ICommandResult? result) = await ReadCommand( monitor, request );
            if( result == null )
            {
                Debug.Assert( cmd != null );
                // The validation return null if no issues occurred, a code V for validation errors and a Code E if an exception occurred
                // (Command validators must not raise any exception).
                result = await GetValidationError( monitor, requestServices, cmd );
            }
            if( result != null )
            {
                // Error can be E or V.
                response.StatusCode = result.Code == VESACode.ValidationError ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
            }
            else
            {
                // Valid command: calls the execution handler.
                Debug.Assert( cmd != null );
                result = await _executor.ExecuteCommandAsync( monitor, requestServices, cmd );
                switch( result.Code )
                {
                    case VESACode.Error: response.StatusCode = StatusCodes.Status500InternalServerError; break;
                    case VESACode.Synchronous: response.StatusCode = StatusCodes.Status200OK; break;
                    default: throw new NotSupportedException( $"VESA code can only be E or S. Code = {result.Code}" );
                }
            }
            using( var writer = new Utf8JsonWriter( response.BodyWriter ) )
            {
                PocoJsonSerializer.Write( result, writer );
            }
        }

        async Task<(ICommand?, ICommandResult?)> ReadCommand( IActivityMonitor monitor, HttpRequest request )
        {
            ICommand? cmd;
            int length = -1;
            using( var buffer = new MemoryStream( 4096 ) )
            {
                try
                {
                    await request.Body.CopyToAsync( buffer );
                    length = (int)buffer.Position;
                    if( length > 0 )
                    {
                        cmd = ReadCommand( monitor, _poco, buffer );
                    }
                    else
                    {
                        return (null, CreateExceptionResult( "Unable to read Command Poco from empty request body.", null, VESACode.ValidationError ));
                    }
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
                        var x = ReadBodyTextOnError( buffer );
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
            return (cmd, null);

            static ICommand? ReadCommand( IActivityMonitor monitor, PocoDirectory p, MemoryStream buffer )
            {
                var reader = new Utf8JsonReader( buffer.GetBuffer().AsSpan( 0, (int)buffer.Position ) );
                var poco = p.ReadPocoValue( ref reader );
                if( poco == null ) throw new InvalidDataException( "Null poco received." );
                if( !(poco is ICommand c) ) throw new InvalidDataException( "Received Poco is not a Command." );
                return c;
            }

            static (string? B, Exception? E) ReadBodyTextOnError( MemoryStream buffer )
            {
                try
                {
                    return (Encoding.UTF8.GetString( buffer.GetBuffer(), 0, (int)buffer.Position ), null );
                }
                catch( Exception ex )
                {
                    return (null,ex);
                }

            }

        }

        async Task<ICommandResult?> GetValidationError( IActivityMonitor monitor, IServiceProvider requestServices, ICommand cmd )
        {
            try
            {
                ValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
                if( !validation.Success )
                {
                    ICommandResult result = _resultFactory.Create();
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

        ICommandResult CreateExceptionResult( string message, Exception? ex, VESACode code )
        {
            ICommandResult result = _resultFactory.Create();
            result.Code = code;
            result.Result = _executor.CreateSimpleErrorResult( message, ex?.Message );
            return result;
        }

    }
}
