using System;

namespace CK.Cris
{
    /// <summary>
    /// Strongly typed executing <typeparamref name="T"/> command.
    /// </summary>
    /// <typeparam name="T">Type of the command.</typeparam>
    public interface IExecutedCommand<T> : IExecutedCommand where T : class, IAbstractCommand
    {
        /// <summary>
        /// Offers strongly types for the both the command and its result.
        /// This must be called only for <see cref="ICommand{TResult}"/> otherwise
        /// an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        public interface WithResult<TResult> : IExecutedCommand<T>
        {
            /// <summary>
            /// Gets the strongly typed result of <see cref="ICommand{TResult}"/>.
            /// </summary>
            new TResult Result { get; }
        }

        /// <summary>
        /// Gets the command that is executing.
        /// </summary>
        new T Command { get; }
    }

}

