using CK.Core;
using System;

namespace CK.Cris
{
    [CKTypeDefiner]
    public interface ICommand<out TResult> : ICommand
    {
    }
}
