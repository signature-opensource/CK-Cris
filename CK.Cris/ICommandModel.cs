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
        /// Creates a new command object.
        /// </summary>
        ICommand Create();

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
        /// Exposes the description of the method that handles a command.
        /// <para>
        /// This should expose the documentation of the method (from Xml generated).
        /// </para>
        /// </summary>
        public interface IHandler
        {
            /// <summary>
            /// Gets the <see cref="IStObjFinalClass"/> that handles this command.
            /// <para>
            /// This should be either a <see cref="IStObjFinalImplementation"/> (for real objects)
            /// or a <see cref="IStObjServiceClassDescriptor"/> (for Automatic Services) but currently
            /// (until the "StObj goes static" is done), this is a dedicated, independent, implementation.
            /// </para>
            /// </summary>
            IStObjFinalClass Type { get; }

            /// <summary>
            /// Gets the name of the method that handles this command.
            /// </summary>
            string MethodName { get; }

            /// <summary>
            /// Gets whether this handler requires the <see cref="ICrisEventSender"/> in its <see cref="Parameters"/>:
            /// it can emit <see cref="ICrisEvent"/>.
            /// </summary>
            bool CanEmitEvents { get; }

            /// <summary>
            /// Gets the parameter types of <see cref="MethodName"/>.
            /// <para>
            /// The array is exposed here to avoid a ToArray on a IReadOnlyList when using these
            /// parameters to find a MethodInfo on a type (that should be this <see cref="Type"/>).
            /// This is a low level API and, of course, the array content must not be
            /// altered.
            /// </para>
            /// </summary>
            Type[] Parameters { get; }
        }

        /// <summary>
        /// Gets the <see cref="IHandler"/> for this command.
        /// When null, no handler has been found and the command cannot be executed in this process.
        /// </summary>
        IHandler? Handler { get; }
    }

}
