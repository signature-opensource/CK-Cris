using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Extends Poco types.
    /// </summary>
    public static class PocoFactoryExtensions
    {
        /// <summary>
        /// Creates a <see cref="ICrisResultError"/> with at least one message.
        /// There can be no <see cref="UserMessageLevel.Error"/> messages in the messages: this result is still an error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="first">The required first message. Must be <see cref="UserMessage.IsValid"/>.</param>
        /// <param name="others">Optional other messages.</param>
        /// <returns>An error result.</returns>
        public static ICrisResultError Create( this IPocoFactory<ICrisResultError> @this, UserMessage first, params UserMessage[] others )
        {
            Throw.CheckArgument( first.IsValid );
            var r = @this.Create();
            r.Messages.Add( first );
            r.Messages.AddRange( others );
            return r;
        }

        /// <summary>
        /// Helper that centralizes unhandled exception behavior.
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="currentCulture">The current culture.</param>
        /// <param name="isExecuting">Whether the command is executing or validating.</param>
        /// <param name="ex">The unhandled exception.</param>
        /// <param name="cmd">The faulted command.</param>
        /// <param name="collector">Error message collector.</param>
        /// <param name="leakAll">
        /// Whether all exceptions must be exposed or only the <see cref="MCException"/> ones.
        /// Defaults to <see cref="CoreApplicationIdentity.EnvironmentName"/> == "#Dev".
        /// </param>
        /// <returns>The log key.</returns>
        public static string OnUnhandledError( IActivityMonitor monitor,
                                               CurrentCultureInfo? currentCulture,
                                               bool isExecuting,
                                               Exception ex,
                                               IAbstractCommand cmd,
                                               List<UserMessage> collector,
                                               bool? leakAll = null )
        {
            var logText = $"While {(isExecuting ? "execu" : "valida")}ting command '{cmd.CrisPocoModel.PocoName}'.";
            using var g = monitor.UnfilteredOpenGroup( LogLevel.Error | LogLevel.IsFiltered, CrisDirectory.CrisTag, logText, null );
            Throw.DebugAssert( !g.IsRejectedGroup );
            if( currentCulture == null )
            {
                MCString m = MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault, logText );
                collector.Add( new UserMessage( UserMessageLevel.Error, m, 0 ) );
            }
            else
            {
                collector.Add( isExecuting
                                ? UserMessage.Error( currentCulture,
                                                     $"An unhandled error occurred while executing command '{cmd.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).",
                                                     "Cris.UnhandledExecutionError" )
                                : UserMessage.Error( currentCulture,
                                                     $"An unhandled error occurred while validating command '{cmd.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).",
                                                     "Cris.UnhandledValidationError" ) );
            }
            var all = leakAll ?? CoreApplicationIdentity.IsInitialized
                                    ? CoreApplicationIdentity.Instance.EnvironmentName == CoreApplicationIdentity.DefaultEnvironmentName
                                    : true;
            if( all )
            {
                if( currentCulture != null )
                {
                    ex.GetUserMessages( currentCulture, collector.Add );
                }
                else
                {
                    ex.GetUserMessages( collector.Add );
                }
            }
            else
            {
                CollectMCOnly( collector.Add, 0, ex );
            }
            // Always logged since we opened an Error group.
            monitor.Info( cmd.ToString()!, ex );
            return g.GetLogKeyString()!;
        }

        static void CollectMCOnly( Action<UserMessage> collector, byte depth, Exception e )
        {
            if( e is AggregateException a )
            {
                ++depth;
                foreach( var sub in a.InnerExceptions ) CollectMCOnly( collector, depth, sub );
            }
            else
            {
                if( e is MCException mC )
                {
                    collector( mC.AsUserMessage().With( depth ) );
                }
                if( e.InnerException != null ) CollectMCOnly( collector, ++depth, e.InnerException );
            }
        }
    }
}
