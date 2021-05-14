using CK.Cris;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Auth
{
    /// <summary>
    /// Defines the <see cref="ActorId"/> field.
    /// This is the most basic command part that can be used to authenticate a command in <see cref="AuthLevel.Normal"/>
    /// or <see cref="AuthLevel.Critical"/> authentication levels.
    /// </summary>
    public interface ICommandAuthenticated : ICommandPart
    {
        /// <summary>
        /// Gets or sets the actor identifier.
        /// The default <see cref="CrisAuthenticationService"/> validates this field against the current <see cref="IAuthenticationInfo"/>.
        /// </summary>
        int ActorId { get; set; }
    }
}
