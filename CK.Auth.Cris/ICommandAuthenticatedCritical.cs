using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Marker interface for commands that require the <see cref="AuthLevel.Critical"/> level to be validated.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandAuthenticatedCritical : ICommandAuthenticated
    {
    }
}
