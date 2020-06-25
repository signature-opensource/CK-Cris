using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Cris
{
    /// <summary>
    /// Describes command properties and its unique and zero-based index in a context.
    /// </summary>
    public interface ICommandModel
    {
        /// <summary>
        /// Gets the command type: this is the final type that implements the <see cref="IPoco"/> command.
        /// </summary>
        Type CommandType { get; }

        /// <summary>
        /// Creates a command object by using the <see cref="IPocoFactory{T}"/> of
        /// the <see cref="CommandType"/>.
        /// </summary>
        ICommand CreateInstance();

        /// <summary>
        /// Gets the command index.
        /// </summary>
        int CommandIdx { get; }

        /// <summary>
        /// Gets the command name.
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Gets the command previous names if any.
        /// </summary>
        IReadOnlyList<string> PreviousNames { get; }

        /// <summary>
        /// Gets the final (most specialized) result type.
        /// This is typeof(void) when no <see cref="ICommand{TResult}"/> exists.
        /// </summary>
        Type ResultType { get; }

        /// <summary>
        /// Gets the handler for this this command.
        /// When null, no handler has been found and the command cannot be executed in this process.
        /// </summary>
        MethodInfo? Handler { get; }

    }
}
