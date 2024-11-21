using System;

namespace CK.Cris;

/// <summary>
/// Strongly typed executed <typeparamref name="T"/> command.
/// </summary>
/// <typeparam name="T">Type of the command.</typeparam>
public interface IExecutedCommand<out T> : IExecutedCommand where T : class, IAbstractCommand
{
    /// <summary>
    /// Offers strongly types for the both the command and its result.
    /// This must be called only for <see cref="ICommand{TResult}"/> and with a result type
    /// compliant with the actual command result's type otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    public interface IWithResult<out TResult> : IExecutedCommand<T>
    {
        /// <summary>
        /// Gets the strongly typed result of <see cref="ICommand{TResult}"/>.
        /// </summary>
        new TResult Result { get; }
    }

    /// <summary>
    /// Gets the command that has been executed.
    /// </summary>
    new T Command { get; }

    /// <summary>
    /// Gets the strongly typed command and its result.
    /// This must be called only for <see cref="ICommand{TResult}"/> otherwise
    /// an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>A strongly typed command and its result.</returns>
    IWithResult<TResult> WithResult<TResult>();

}

