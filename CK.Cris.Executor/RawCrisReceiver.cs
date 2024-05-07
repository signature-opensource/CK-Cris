using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Command receiver service.
    /// <para>
    /// This is a processwide singleton that can validate the incoming commands and
    /// configure ambient services to execute the command in another DI container. 
    /// </para>
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.Cris.RawCrisReceiverImpl, CK.Cris.Executor.Engine" )]
    // To simplify testing.
    [AlsoRegisterType( typeof( NormalizedCultureInfoUbiquitousServiceDefault ) )]
    [AlsoRegisterType( typeof( TranslationService ) )]
    [AlsoRegisterType( typeof( NormalizedCultureInfo ) )]
    [AlsoRegisterType( typeof( CurrentCultureInfo ) )]
    public abstract class RawCrisReceiver : ISingletonAutoService
    {
        /// <summary>
        /// Validates a command by calling all the discovered [CommandIncomingValidator] validators.
        /// <para>
        /// This never throws: exceptions are handled (logged and appear in the error messages) by this method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <param name="commandLogGroup">Optional opened group for the command handling.</param>
        /// <param name="currentCulture">Optional culture to use instead of the one from <paramref name="services"/>.</param>
        /// <returns>The validation result.</returns>
        public async Task<CrisValidationResult> IncomingValidateAsync( IActivityMonitor monitor,
                                                                       IServiceProvider services,
                                                                       IAbstractCommand command,
                                                                       IDisposableGroup? commandLogGroup = null,
                                                                       CurrentCultureInfo? currentCulture = null )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( services );
            Throw.CheckNotNullArgument( command );

            // We handle the (unexpected) case where the CurrentCultureInfo or the TranslationService is not
            // available in the DI by catching the GetRequiredService exception.
            try
            {
                currentCulture = HandleCulture( monitor, services, command, currentCulture );
                var c = new UserMessageCollector( currentCulture );
                var hub = await DoIncomingValidateAsync( monitor, c, services, command );
                if( c.ErrorCount > 0 )
                {
                    string logKey = LogValidationError( monitor, command, c, "incoming", commandLogGroup );
                    return new CrisValidationResult( c.UserMessages, null, logKey );
                }
                return c.UserMessages.Count == 0
                        ? (hub == null ? CrisValidationResult.SuccessResult : new CrisValidationResult( hub, null ))
                        : new CrisValidationResult( c.UserMessages, hub, null );
            }
            catch( Exception ex )
            {
                var messages = ImmutableArray.CreateBuilder<UserMessage>();
                var k = PocoFactoryExtensions.OnUnhandledError( monitor, ex, command, false, currentCulture, messages.Add );
                return new CrisValidationResult( messages.ToImmutableArray(), null, k );
            }
        }

        static protected CurrentCultureInfo HandleCulture( IActivityMonitor monitor, IServiceProvider services, ICrisPoco crisPoco, CurrentCultureInfo? currentCulture )
        {
            if( crisPoco is ICurrentCulturePart cC && !String.IsNullOrWhiteSpace( cC.CurrentCultureName ) )
            {
                // Do not use EnsureExtendedCultureInfo here. We don't want to be flood by random strings
                // that will damage the cache.
                var fromCommand = ExtendedCultureInfo.FindExtendedCultureInfo( cC.CurrentCultureName );
                if( fromCommand != null )
                {
                    currentCulture = new CurrentCultureInfo( services.GetRequiredService<TranslationService>(), fromCommand );
                }
                else
                {
                    monitor.Warn( $"Unexisting CurrentCultureName '{cC.CurrentCultureName}' while validating command '{crisPoco.CrisPocoModel.PocoName}'. Ignoring it." );
                }
            }
            currentCulture ??= services.GetRequiredService<CurrentCultureInfo>();
            return currentCulture;
        }

        // Also used by Command validators.
        internal static string LogValidationError( IActivityMonitor monitor,
                                                   ICrisPoco command,
                                                   UserMessageCollector c,
                                                   string incomingOrHandling,
                                                   IDisposableGroup? commandLogGroup )
        {
            // Don't open a new group if there's one and it is not rejected.
            bool errorOpened = false;
            if( commandLogGroup == null || commandLogGroup.IsRejectedGroup )
            {
                errorOpened = true;
                commandLogGroup = monitor.UnfilteredOpenGroup( LogLevel.Error | LogLevel.IsFiltered, CrisDirectory.CrisTag, $"Command '{command.CrisPocoModel.PocoName}' {incomingOrHandling} validation error.", null );
            }
            string? logKey = commandLogGroup.GetLogKeyString();
            Throw.DebugAssert( "The group is not rejected.", logKey != null );
            c.DumpLogs( monitor );
            if( errorOpened ) monitor.CloseGroup();
            return logKey;
        }

        /// <summary>
        /// This method is automatically implemented by RawCrisValidatorImpl in CK.Cris.Executor.Engine.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="validationContext">The user message collector.</param>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to validate.</param>
        /// <returns>The ambient service hub if it is needed.</returns>
        protected abstract ValueTask<AmbientServiceHub?> DoIncomingValidateAsync( IActivityMonitor monitor,
                                                                                  UserMessageCollector validationContext,
                                                                                  IServiceProvider services,
                                                                                  IAbstractCommand command );


    }
}
