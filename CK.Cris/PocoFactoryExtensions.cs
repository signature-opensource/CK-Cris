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
        /// Creates a <see cref="ISimpleCrisResultError"/> from a <see cref="ICrisResultError"/>.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="error">The source error result.</param>
        /// <returns>A simple error result.</returns>
        [return: NotNullIfNotNull( nameof( error ) )]
        public static ISimpleCrisResultError? Create( this IPocoFactory<ISimpleCrisResultError> @this, ICrisResultError? error )
        {
            if( error == null ) return null;
            var r = @this.Create();
            r.IsValidationError = error.IsValidationError;
            r.LogKey = error.LogKey;
            foreach( var m in error.Messages ) r.Messages.Add( m );
            return r;
        }

        /// <summary>
        /// Creates a <see cref="ISimpleCrisResultError"/> with at least one message.
        /// There can be no <see cref="UserMessageLevel.Error"/> messages in the messages: this result is still an error.
        /// </summary>
        /// <param name="this">This factory.</param>
        /// <param name="first">The required first message. Must be <see cref="SimpleUserMessage.IsValid"/>.</param>
        /// <param name="others">Optional other messages.</param>
        /// <returns>An error result.</returns>
        public static ISimpleCrisResultError Create( this IPocoFactory<ISimpleCrisResultError> @this, SimpleUserMessage first, params SimpleUserMessage[] others )
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
        /// <param name="genericError">A generic error message.</param>
        /// <returns>The log key.</returns>
        public static string OnUnhandledError( IActivityMonitor monitor,
                                               CurrentCultureInfo currentCulture,
                                               bool isExecuting,
                                               Exception ex,
                                               IAbstractCommand cmd,
                                               out UserMessage genericError )
        {
            using var g = monitor.UnfilteredOpenGroup( LogLevel.Error | LogLevel.IsFiltered,
                                                       CrisDirectory.CrisTag,
                                                       $"While {(isExecuting ? "execu" : "valida")}ting command '{cmd.CrisPocoModel.PocoName}'.", null );
            Throw.DebugAssert( !g.IsRejectedGroup );
            genericError = isExecuting
                            ? UserMessage.Error( currentCulture, $"An unhandled error occurred while executing command '{cmd.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).", "Cris.UnhandledExecutionError" )
                            : UserMessage.Error( currentCulture, $"An unhandled error occurred while validating command '{cmd.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).", "Cris.UnhandledValidationError" );
            // Always logged since we opened an Error group.
            monitor.Info( cmd.ToString()!, ex );
            return g.GetLogKeyString()!;
        }

    }
}
