using CK.Core;
using CK.Cris.AmbientValues;
using CK.Poco.Exc.Json;
using CK.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Cris.AspNet
{
    [EndpointSingletonService]
    [AlsoRegisterType( typeof( CrisDirectory ) )]
    [AlsoRegisterType( typeof( TypeScriptCrisCommandGenerator ) )]
    [AlsoRegisterType( typeof( CommonPocoJsonSupport ) )]
    [AlsoRegisterType( typeof( RawCrisValidator ) )]
    [AlsoRegisterType( typeof( IAspNetCrisResult ) )]
    [AlsoRegisterType( typeof( IAspNetCrisResultError ) )]
    [AlsoRegisterType( typeof( CrisBackgroundExecutorService ) )]
    [AlsoRegisterType( typeof( IAmbientValuesCollectCommand ) )]
    public partial class CrisAspNetService : ISingletonAutoService
    {
        readonly RawCrisValidator _validator;
        readonly CrisBackgroundExecutorService _backgroundExecutor;
        internal readonly PocoDirectory _pocoDirectory;
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
            _pocoDirectory = poco;
            _validator = validator;
            _backgroundExecutor = backgroundExecutor;
            _resultFactory = resultFactory;
            _errorResultFactory = errorResultFactory;
            _crisErrorResultFactory = backendErrorResultFactory;
        }

        /// <summary>
        /// Handles a command request parsed with the provided <paramref name="reader"/>: the command is
        /// validated and executed by the <see cref="CrisBackgroundExecutorService"/> or inline if it can.
        /// <para>
        /// Any specific input processing or pre processing can be done by the <paramref name="reader"/> function.
        /// Output of the <see cref="IAspNetCrisResult"/> MUST use the returned TypeFilterName.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="request">The http request.</param>
        /// <param name="reader">The payload reader.</param>
        /// <param name="useSimpleError">False to keep <see cref="ICrisResultError"/> instead of the simpler <see cref="IAspNetCrisResultError"/>.</param>
        /// <param name="currentCultureInfo">Optional current culture.</param>
        /// <returns>The command result and the <see cref="ExchangeableRuntimeFilter.Name"/> if it is valid.</returns>
        public Task<(IAspNetCrisResult Result, string TypeFilterName)> HandleRequestAsync( IActivityMonitor monitor,
                                                                                           HttpRequest request,
                                                                                           CommandRequestReader reader,
                                                                                           bool useSimpleError = true,
                                                                                           CurrentCultureInfo? currentCultureInfo = null )
        {
            return DoHandleAsync( monitor, request, reader, useSimpleError, currentCultureInfo, null );
        }

        internal async Task<(IAspNetCrisResult Result, string TypeFilterName)> DoHandleAsync( IActivityMonitor monitor,
                                                                                              HttpRequest request,
                                                                                              CommandRequestReader reader,
                                                                                              bool useSimpleError,
                                                                                              CurrentCultureInfo? currentCultureInfo,
                                                                                              PocoJsonImportOptions? readOptions )
        {
            // There is no try catch here and this is intended. An unhandled exception here
            // is an Internal Server Error that should bubble up.
            var requestServices = request.HttpContext.RequestServices;
            using( HandleIncomingCKDepToken( monitor, request, out var depToken ) )
            {
                // If we cannot read the command, it is considered as a Validation error.
                (IAbstractCommand? cmd, IAspNetCrisResult? result, string? typeFilterName) = await ReadCommandAsync( monitor,
                                                                                                                     request,
                                                                                                                     reader,
                                                                                                                     currentCultureInfo,
                                                                                                                     readOptions );
                // No result => no validation error (and we have a valid TypeFilterName).
                if( result == null )
                {
                    Throw.DebugAssert( cmd != null && typeFilterName != null );
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
                if( useSimpleError && result.Result is ICrisResultError error )
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
                return (result, typeFilterName ?? "TypeScript");
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


        async Task<(IAbstractCommand? Command, IAspNetCrisResult? Error, string? TypeFilterName)> ReadCommandAsync( IActivityMonitor monitor,
                                                                                                                    HttpRequest request,
                                                                                                                    CommandRequestReader reader,
                                                                                                                    CurrentCultureInfo? currentCultureInfo,
                                                                                                                    PocoJsonImportOptions? readOptions )
        {
            currentCultureInfo ??= request.HttpContext.RequestServices.GetRequiredService<CurrentCultureInfo>();
            var messageCollector = new UserMessageCollector( currentCultureInfo );
            if( readOptions == null && !TryCreateJsonImportOptions( request, messageCollector, out readOptions ) )
            {
                return (null, CreateValidationErrorResult( messageCollector, null ), null);
            }
            int length = -1;
            using( var buffer = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream() )
            {
                try
                {
                    await request.Body.CopyToAsync( buffer );
                    length = (int)buffer.Position;
                    if( length > 0 )
                    {
                        var cmd = await reader( monitor, request, _pocoDirectory, messageCollector, buffer.GetReadOnlySequence(), readOptions );
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
                            return (cmd, null, readOptions.TypeFilterName);
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
                    return (null, CreateValidationErrorResult( messageCollector, null ), null);
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
                    return (null, CreateValidationErrorResult( messageCollector, gError.GetLogKeyString() ), null);
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
        /// Helper that reads optional "UsePascalCase", "Indented", "TypeLess", "UnsafeRelaxedJsonEscaping"
        /// and optionally "SkipValidation" keys from request's query to build the options that can be used
        /// to write the json result.
        /// <para>
        /// Using the <see cref="System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/> is not recommended. 
        /// </para>
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="typeFilterName">
        /// The <see cref="ExchangeableRuntimeFilter.Name"/> to use.
        /// Must be or start with "TypeScript" otherwise an <see cref="ArgumentException"/> is thrown.
        /// </param>
        /// <param name="skipValidation">
        /// It is safer for <see cref="JsonWriterOptions.SkipValidation"/> to be false except when using generated serialization code.
        /// Since this helper doesn't know how the Json will actually be serialized, this defaults to false.
        /// By setting this to null, it is up to the caller to decide whether json validation must be skipped or not. 
        /// </param>
        /// <returns>The export options to use.</returns>
        public virtual PocoJsonExportOptions CreateJsonExportOptions( HttpRequest request,
                                                                      string typeFilterName,
                                                                      bool? skipValidation = false )
        {
            Throw.CheckArgument( typeFilterName.StartsWith( "TypeScript" ) );
            bool usePascalCase = request.Query["UsePascalCase"].Any();
            bool indented = request.Query["Indented"].Any();
            bool typeLess = request.Query["TypeLess"].Any();
            if( !skipValidation.HasValue ) skipValidation = request.Query["SkipValidation"].Any();
            var encoder = request.Query["UnsafeRelaxedJsonEscaping"].Any()
                            ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                            : System.Text.Encodings.Web.JavaScriptEncoder.Default;
            var o = new PocoJsonExportOptions()
            {
                TypeFilterName = typeFilterName,
                UseCamelCase = !usePascalCase,
                TypeLess = typeLess,
                WriterOptions = new JsonWriterOptions()
                {
                    Encoder = encoder,
                    Indented = indented,
                    SkipValidation = skipValidation.Value
                }
            };
            return o;
        }

        /// <summary>
        /// Helper that handles optional "TypeFilterName" and "AllowTrailingCommas" query string arguments
        /// to initialize a new <see cref="PocoJsonImportOptions"/>.
        /// The <see cref="PocoJsonImportOptions.TypeFilterName"/> must start with "TypeScript".
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="messageCollector">The user message collector.</param>
        /// <param name="importOptions">The resulting import options.</param>
        /// <returns>True on success, false on error.</returns>
        static bool TryCreateJsonImportOptions( HttpRequest request,
                                                UserMessageCollector messageCollector,
                                                [NotNullWhen( true )] out PocoJsonImportOptions? importOptions )
        {
            var typeFilterName = (string?)request.Query["TypeFilterName"];
            if( typeFilterName == null ) typeFilterName = "TypeScript";
            else
            {
                if( !typeFilterName.StartsWith( "TypeScript" ) )
                {
                    messageCollector.Error( $"Invalid TypeFilterName '{typeFilterName}'.", "Cris.AspNet.InvalidTypeFilterName" );
                    importOptions = null;
                    return false;
                }
            }
            bool allowTrailingCommas = request.Query["AllowTrailingCommas"].Any();
            importOptions = new PocoJsonImportOptions()
            {
                ReaderOptions = new JsonReaderOptions() { AllowTrailingCommas = allowTrailingCommas },
                TypeFilterName = typeFilterName
            };
            return true;
        }

        /// <summary>
        /// Standard <see cref="CommandRequestReader"/> of a payload that is a <see cref="IAbstractCommand"/> poco in JSON format.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="request">The http request.</param>
        /// <param name="pocoDirectory">The poco directory.</param>
        /// <param name="messageCollector">The message collector to use for errors, warnings and logs.</param>
        /// <param name="payload">The request payload.</param>
        /// <param name="readOptions">
        /// The reader options. <see cref="PocoJsonImportOptions.TypeFilterName"/> must start with "TypeScript" otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </param>
        /// <returns>
        /// A non null command on success. When null, at least one <see cref="UserMessageLevel.Error"/> message should be in the collector.
        /// </returns>
        public static ValueTask<IAbstractCommand?> StandardReadCommandAsync( IActivityMonitor monitor,
                                                                             HttpRequest request,
                                                                             PocoDirectory pocoDirectory,
                                                                             UserMessageCollector messageCollector,
                                                                             ReadOnlySequence<byte> payload,
                                                                             PocoJsonImportOptions readOptions )
        {
            Throw.CheckArgument( readOptions.TypeFilterName.StartsWith( "TypeScript" ) );
            return DoStandardReadAsync( monitor, request, pocoDirectory, messageCollector, payload, readOptions );
        }

        internal static ValueTask<IAbstractCommand?> DoStandardReadAsync( IActivityMonitor monitor,
                                                                          HttpRequest request,
                                                                          PocoDirectory pocoDirectory,
                                                                          UserMessageCollector messageCollector,
                                                                          ReadOnlySequence<byte> payload,
                                                                          PocoJsonImportOptions readOptions )
        {
            var poco = pocoDirectory.ReadJson( payload, readOptions );
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
