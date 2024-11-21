using CK.Core;

namespace CK.Cris;

/// <summary>
///  A command that delays the execution of another <see cref="Command"/>.
/// </summary>
public interface IDelayedCommand : ICommand
{
    /// <summary>
    /// Gets or sets the execution date of the <see cref="Command"/>.
    /// </summary>
    DateTime ExecutionDate { get; set; }

    /// <summary>
    /// Gets or sets whether a past <see cref="ExecutionDate"/> immediately executes the command.
    /// <para>
    /// By default, a date in the past triggers an incoming validation error.
    /// </para>
    /// </summary>
    bool AllowPastExecutionDate { get; set; }

    /// <summary>
    /// Gets or sets whether the command must only be kept in memory.
    /// <para>
    /// By default, if the service that implements delayed command execution is a persistent one,
    /// the command is persisted.  
    /// </para>
    /// </summary>
    bool KeepOnlyInMemory { get; set; }

    /// <summary>
    /// Gets or sets the command to execute at <see cref="ExecutionDate"/>.
    /// </summary>
    [NullInvalid]
    IAbstractCommand? Command { get; set; }
}
