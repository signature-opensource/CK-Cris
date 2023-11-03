using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Command validation service.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.Cris.RawCrisValidatorImpl, CK.Cris.Executor.Engine" )]
    // To simplify testing.
    [AlsoRegisterType( typeof( NormalizedCultureInfoUbiquitousServiceDefault ) )]
    [AlsoRegisterType( typeof( TranslationService ) )]
    [AlsoRegisterType( typeof( NormalizedCultureInfo ) )]
    [AlsoRegisterType( typeof( CurrentCultureInfo ) )]
    public abstract class RawCrisValidator : ISingletonAutoService
    {
        protected CrisDirectory Directory;

        /// <summary>
        /// Initializes a new <see cref="RawCrisValidator"/>.
        /// </summary>
        /// <param name="directory">The command directory.</param>
        public RawCrisValidator( CrisDirectory directory )
        {
            Directory = directory;
        }

        /// <summary>
        /// Validates a command by calling all the discovered validators.
        /// <para>
        /// Validation failures are logged and exceptions are handled (and logged) by this method,
        /// see <see cref="CrisValidationResult(Exception,UserMessage,string)"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <param name="commandLogGroup">Optional opened group for the command handling.</param>
        /// <returns>The validation result.</returns>
        public async Task<CrisValidationResult> ValidateCommandAsync( IActivityMonitor monitor,
                                                                      IServiceProvider services,
                                                                      IAbstractCommand command,
                                                                      IDisposableGroup? commandLogGroup = null )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( services );
            Throw.CheckNotNullArgument( command );

            // The CrisValidationResult.LogKey will be this one if it has been opened.
            // Otherwise it will be null on success and the error group log key on error.
            using var g = commandLogGroup ?? monitor.OpenInfo( CrisDirectory.CrisTag, $"Validating '{command.CrisPocoModel.PocoName}' command." );
            // We handle the case where the CurrentCultureInfo is not available in the DI.
            CurrentCultureInfo? currentCulture = null;
            try
            {
                currentCulture = services.GetRequiredService<CurrentCultureInfo>();
                var c = new UserMessageCollector( currentCulture );
                await DoValidateCommandAsync( monitor, c, services, command );
                if( c.ErrorCount > 0 )
                {
                    // Don't open a new group if there's one.
                    string? logKey = g.GetLogKeyString() ?? monitor.OpenError( CrisDirectory.CrisTag, $"Command '{command.CrisPocoModel.PocoName}' validation error." ).GetLogKeyString();
                    c.DumpLogs( monitor );
                    if( g.IsRejectedGroup ) monitor.CloseGroup();
                    return new CrisValidationResult( c, logKey );
                }
                return c.UserMessages.Count == 0
                        ? CrisValidationResult.SuccessResult
                        : new CrisValidationResult( c, g.GetLogKeyString() );
            }
            catch( Exception ex )
            {
                var messages = new List<UserMessage>();
                var k = PocoFactoryExtensions.OnUnhandledError( monitor, currentCulture, false, ex, command, messages );
                return new CrisValidationResult( messages, g.GetLogKeyString() ?? k );
            }
        }

        /// <summary>
        /// This method is automatically implemented by RawCrisValidatorImpl in CK.Cris.Executor.Engine.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <returns>The awaitable.</returns>
        protected abstract Task DoValidateCommandAsync( IActivityMonitor monitor,
                                                        UserMessageCollector validationContext,
                                                        IServiceProvider services,
                                                        IAbstractCommand command );
    }
}
