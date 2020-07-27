using CK.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
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
        readonly FrontCommandExecutor _executor;
        readonly PocoDirectory _poco;
        readonly IPocoFactory<ICommandResult> _resultFactory;

        public CrisAspNetService( PocoDirectory poco, CommandValidator validator, FrontCommandExecutor executor, IPocoFactory<ICommandResult> resultFactory )
        {
            _poco = poco;
            _validator = validator;
            _executor = executor;
            _resultFactory = resultFactory;
        }

        public async Task HandleRequest( IActivityMonitor monitor, IServiceProvider requestServices, HttpRequest request, HttpResponse response )
        {
            ICommand? cmd;
            using( var buffer = new MemoryStream( 4096 ) )
            {
                await request.Body.CopyToAsync( buffer );
                cmd = ReadCommand( monitor, _poco, buffer );
            }
            if( cmd == null )
            {
                response.StatusCode = StatusCodes.Status400BadRequest;
            }
            else
            {
                ICommandResult result;
                DateTime startValidation = DateTime.UtcNow;
                ValidationResult validation = await _validator.ValidateCommandAsync( monitor, requestServices, cmd );
                if( !validation.Success )
                {
                    response.StatusCode = StatusCodes.Status406NotAcceptable;
                    result = _resultFactory.Create();
                    result.Code = VESACode.ValidationError;
                    result.Result = validation.Errors.ToList();
                }
                else
                {
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
                    writer.WriteStartObject();
                    writer.WriteString( "Code", result.Code.ToString() );
                    writer.WritePropertyName( "Result" );
                    if( result.Result is ValidationResult )
                    {
                        writer.WriteStartObject();
                        writer.WriteEndObject();
                    }
                    writer.WriteEndObject();
                }
            }
        }

        static ICommand? ReadCommand( IActivityMonitor m, PocoDirectory p, MemoryStream buffer )
        {
            try
            {
                var reader = new Utf8JsonReader( buffer.GetBuffer().AsSpan( 0, (int)buffer.Position ) );
                var poco = p.ReadPocoValue( ref reader );
                if( poco == null ) m.Error( "Null poco received." );
                return (ICommand?)poco;
            }
            catch( Exception ex )
            {
                m.Error( "Unable to read Poco from body.", ex );
                return null;
            }
        }

    }
}
