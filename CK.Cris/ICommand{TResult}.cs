using CK.Core;
using System;
using System.Reflection;
using System.Security.Cryptography;

namespace CK.Cris
{

    /// <summary>
    /// Describes a type of command that expects a result.
    /// </summary>
    /// <typeparam name="TResult">Type of the expected result.</typeparam>
    [CKTypeDefiner]
    public interface ICommand<out TResult> : ICommand
    {
        internal static TResult R => default!;

        public static PropertyInfo GetResultType() => typeof( ICommand<TResult> ).GetProperty( "R", BindingFlags.Static | BindingFlags.NonPublic )!;
    }


}
