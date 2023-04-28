using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// Intermediate abstraction that tags <see cref="ICommand"/> and <see cref="ICommand{TResult}"/>.
    /// This is not intended to be used directly: <see cref="ICommand"/> and <see cref="ICommand{TResult}"/> must
    /// be used.
    /// </summary>
    [CKTypeDefiner]
    public interface IAbstractCommand : ICrisPoco
    {
    }

}
