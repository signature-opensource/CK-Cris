using CK.Core;

namespace CK.Cris
{
    public sealed partial class CrisExecutionHost
    {
        /// <summary>
        /// The executor returns this result wrapper for 2 reasons:
        /// <list type="bullet">
        /// <item>
        /// This enables serialization to always consider any Poco serialization implementation
        /// and guaranties that only Poco compliant types are returned to the caller.
        /// </item>
        /// <item>
        /// This acts as a union type: the result is a <see cref="ICrisResultError"/>, or any other type
        /// or null. A <see cref="ICommand{TResult}"/> where TResult is a ICrisResultError can return
        /// an error (or null), and if TResult is <c>object?</c>, the handler can return any object,
        /// an error, or null.
        /// </item>
        /// </list>
        /// Defining a nested Poco (and registering it thanks to the <see cref="Setup.AlsoRegisterTypeAttribute"/>) makes
        /// it non extensible (and this is a good thing).
        /// </summary>
        [ExternalName( "CrisJobResult" )]
        public interface ICrisJobResult : IPoco
        {
            /// <summary>
            /// Gets or sets the execution result.
            /// </summary>
            object? Result { get; set; }
        }

    }

}
