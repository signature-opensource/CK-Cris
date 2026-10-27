using System;
using System.Threading.Tasks;

namespace CK.Cris;

/// <summary>
/// Strongly typed executing <typeparamref name="T"/> command.
/// </summary>
/// <typeparam name="T">Type of the command.</typeparam>
public interface IExecutingCommand<T> : IExecutingCommand where T : class, IAbstractCommand
{
    /// <summary>
    /// Offers strongly types for the both the command and its result.
    /// See <see cref="WithResult{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    public interface IResultAdapter<TResult> : IExecutingCommand<T>
    {
        /// <summary>
        /// Gets a task that is completed with a successful result, or with an exception
        /// if <see cref="IExecutedCommand.Result"/> is a <see cref="ICrisResultError"/> or
        /// is not compatible with <typeparamref name="TResult"/> (an <see cref="InvalidCastException"/>).
        /// </summary>
        Task<TResult> Result { get; }
    }

    /// <summary>
    /// Gets the command that is executing.
    /// </summary>
    new T Command { get; }

    /// <summary>
    /// Gets the strongly typed command and its result.
    /// This must be called only for <see cref="ICommand{TResult}"/> otherwise
    /// an <see cref="ArgumentException"/> is thrown.
    /// <para>
    /// <typeparamref name="TResult"/> is not checked against the command's result type: a result can be
    /// adapted to any type its value is compatible with (for instance any interface of its Poco family).
    /// An incompatible result faults the <see cref="IResultAdapter{TResult}.Result"/> task with an <see cref="InvalidCastException"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>A strongly typed command and its result.</returns>
    IExecutingCommand<T>.IResultAdapter<TResult> WithResult<TResult>();
}

