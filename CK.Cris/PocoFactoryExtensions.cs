using CK.Core;
using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Cris;

/// <summary>
/// Extends Poco types.
/// </summary>
public static class PocoFactoryExtensions
{
    /// <summary>
    /// Creates an exception from the <see cref="ICrisResultError.Errors"/>.
    /// </summary>
    /// <param name="e">This result error.</param>
    /// <param name="lineNumber">Calling line number (usually set by Roslyn).</param>
    /// <param name="fileName">Calling file path (usually set by Roslyn).</param>
    /// <returns>The exception.</returns>
    public static CKException CreateException( this ICrisResultError e, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string? fileName = null )
    {
        var msg = e.Errors;
        var b = new StringBuilder();
        for( int iM = 0; iM < msg.Count; iM++ )
        {
            UserMessage m = msg[iM];
            if( m.IsValid )
            {
                var lines = m.Text.Split( '\n', StringSplitOptions.TrimEntries );
                if( lines.Length > 0 )
                {
                    b.Append( ' ', m.Depth * 2 ).Append( "- " ).AppendLine( lines[0] );
                    if( iM == 0 )
                    {
                        b.Append( ' ', m.Depth * 2 ).Append( "  -> " ).Append( fileName ).Append( '@' ).Append( lineNumber ).AppendLine();
                    }
                    for( int i = 1; i < lines.Length; i++ )
                    {
                        b.Append( ' ', m.Depth * 2 ).AppendLine( lines[i] );
                    }
                }
            }
        }
        return new CKException( b.Length == 0 ? "Cris error (no messages)." : b.ToString() );
    }

    /// <summary>
    /// Helper that centralizes unhandled exception behavior.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="ex">The unhandled exception.</param>
    /// <param name="crisPoco">The faulted command or event.</param>
    /// <param name="isExecuting">Whether the command is executing or validating.</param>
    /// <param name="currentCulture">The current culture.</param>
    /// <param name="collector">Error message collector.</param>
    /// <param name="leakAll">
    /// Whether all exceptions must be exposed or only the <see cref="MCException"/> ones.
    /// Defaults to <see cref="CoreApplicationIdentity.EnvironmentName"/> == "#Dev".
    /// </param>
    /// <returns>The log key.</returns>
    public static string OnUnhandledError( IActivityMonitor monitor,
                                           Exception ex,
                                           ICrisPoco crisPoco,
                                           bool isExecuting,
                                           CurrentCultureInfo? currentCulture,
                                           Action<UserMessage> collector,
                                           bool? leakAll = null )
    {
        var logText = $"While {(isExecuting ? "execu" : "valida")}ting '{crisPoco.CrisPocoModel.PocoName}'.";
        using var g = monitor.UnfilteredOpenGroup( LogLevel.Error | LogLevel.IsFiltered, CrisDirectory.CrisTag, logText, null );
        Throw.DebugAssert( !g.IsRejectedGroup );
        if( currentCulture == null )
        {
            MCString m = MCString.CreateNonTranslatable( NormalizedCultureInfo.CodeDefault, logText );
            collector( new UserMessage( UserMessageLevel.Error, m, 0 ) );
        }
        else
        {
            collector( isExecuting
                            ? UserMessage.Error( currentCulture,
                                                 $"An unhandled error occurred while executing command '{crisPoco.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).",
                                                 "Cris.UnhandledExecutionError" )
                            : UserMessage.Error( currentCulture,
                                                 $"An unhandled error occurred while validating command '{crisPoco.CrisPocoModel.PocoName}' (LogKey: {g.GetLogKeyString()}).",
                                                 "Cris.UnhandledValidationError" ) );
        }
        ex.GetUserMessages( collector, currentCulture, 0, null, leakAll );
        // Always logged since we opened an Error group.
        monitor.Info( crisPoco.ToString()!, ex );
        return g.GetLogKeyString()!;
    }
}
