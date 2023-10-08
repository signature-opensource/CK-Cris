using CK.Core;
using System;
using System.Reflection;
using System.Security.Cryptography;

namespace CK.Cris
{

    /// <summary>
    /// Describes a type of command that expects a result.
    /// Any type that extends this interface defines a new command type.
    /// Command type names should keep the initial "I" (of the interface) and
    /// end with "Command".
    /// </summary>
    /// <typeparam name="TResult">Type of the expected result.</typeparam>
    public interface ICommand<out TResult> : IAbstractCommand
    {
        internal static TResult R => default!;
    }


}
