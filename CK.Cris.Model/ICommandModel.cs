using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Describes command properties and its unique and zero-based index in a context.
    /// </summary>
    public interface ICommandModel
    {
        /// <summary>
        /// Gets the command type: this is the closing interface of
        /// the <see cref="IClosedPoco"/> command.
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
        /// Gets whether this command has an associated handler.
        /// When false, no handler has been found and the command cannot be executed in this process.
        /// </summary>
        bool HasHandler { get; }

    }
}
