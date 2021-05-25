using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Extends the basic <see cref="ICommandAuthUnsafe"/> to add the <see cref="ActualActorId"/> field.
    /// </summary>
    [CKTypeDefiner]
    public interface ICommandAuthImpersonation : ICommandAuthUnsafe
    {
        /// <summary>
        /// Gets or sets the actual actor identifier: the one that is connected, regardless of any impersonation.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the current <see cref="IAuthenticationInfo.ActualUser"/>.
        /// </summary>
        int ActualActorId { get; set; }
    }
}
