using CK.Core;
using System;

namespace CK.Cris
{
    /// <summary>
    /// The base command interface marker is a <see cref="IClosedPoco"/>.
    /// Any type that extends this interface defines a new command type.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommand : IClosedPoco
    {
    }
}
