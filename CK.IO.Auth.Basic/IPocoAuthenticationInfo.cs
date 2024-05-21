using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Auth
{
    /// <summary>
    /// Flattened view of the <see cref="IAuthenticationInfo"/>.
    /// </summary>
    public interface IPocoAuthenticationInfo : IPoco
    {
        /// <summary>
        /// See <see cref="IAuthenticationInfo.User"/>.
        /// </summary>
        IPocoUserInfo User { get; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.UnsafeUser"/>.
        /// </summary>
        IPocoUserInfo UnsafeUser { get; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.ActualUser"/>.
        /// </summary>
        IPocoUserInfo ActualUser { get; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.IsImpersonated"/>.
        /// </summary>
        bool IsImpersonated { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.Level"/>.
        /// </summary>
        AuthLevel Level { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.Expires"/>.
        /// </summary>
        DateTime? Expires { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.CriticalExpires"/>.
        /// </summary>
        DateTime? CriticalExpires { get; set; }

        /// <summary>
        /// Initializes this Poco from an actual <see cref="IAuthenticationInfo"/>.
        /// </summary>
        /// <param name="info">The actual information.</param>
        void InitializeFrom( IAuthenticationInfo info )
        {
            Throw.CheckNotNullArgument( info );
            User.InitializeFrom( info.User );
            ActualUser.InitializeFrom( info.ActualUser );
            UnsafeUser.InitializeFrom( info.UnsafeUser );
            IsImpersonated = info.IsImpersonated;
            Level = info.Level;
            Expires = info.Expires;
            CriticalExpires = info.CriticalExpires;
        }
    }
}
