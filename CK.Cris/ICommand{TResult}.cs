using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Decribes a type of command that expects a result.
    /// </summary>
    /// <typeparam name="TResult">Type of the expected result.</typeparam>
    [CKTypeDefiner]
    public interface ICommand<out TResult> : ICommand
    {
    }


}
