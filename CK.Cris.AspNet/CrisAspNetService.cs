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

        private async Task<(ICommand?, ICommandResult?)> ReadCommand( IActivityMonitor monitor, HttpRequest request )
        {
            ICommand? cmd;
            int length = -1;
            using( var buffer = new MemoryStream( 4096 ) )
            {
                try
                {
                    await request.Body.CopyToAsync( buffer );
                    (cmd, length) = ReadCommand( monitor, _poco, buffer );
                }
                catch( Exception ex )
                {
                    var message = $"Unable to read Poco from request body (byte length = {length}.";
                    using( monitor.OpenError( message, ex ) )
                    {
                        monitor.Trace( Encoding.UTF8.GetString( buffer.GetBuffer(), 0, length ) );
                    }
                    return (null, CreateExceptionResult( message, ex, VESACode.ValidationError ));
                }
            }
            return (cmd, null);

            static (ICommand?, int) ReadCommand( IActivityMonitor monitor, PocoDirectory p, MemoryStream buffer )
            {
                int length = (int)buffer.Position;
                var reader = new Utf8JsonReader( buffer.GetBuffer().AsSpan( 0, length ) );
                var poco = p.ReadPocoValue( ref reader );
                if( poco == null ) throw new InvalidDataException( "Null poco received." );
                return ((ICommand?)poco, length);
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

        ICommandResult CreateExceptionResult( string message, Exception ex, VESACode code )
        {
            ICommandResult result = _resultFactory.Create();
            result.Code = code;
            result.Result = _executor.CreateSimpleErrorResult( message, ex.Message );
            return result;
        }

    }
}
