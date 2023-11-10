using CK.Core;
using System;

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
        string UserName { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.User"/>.
        /// </summary>
        int UserId { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.UnsafeUser"/>.
        /// </summary>
        string UnsafeUserName { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.UnsafeUser"/>.
        /// </summary>
        int UnsafeUserId { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.ActualUser"/>.
        /// </summary>
        string ActualUserName { get; set; }

        /// <summary>
        /// See <see cref="IAuthenticationInfo.ActualUser"/>.
        /// </summary>
        int ActualUserId { get; set; }

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
        /// Initializes this from an actual <see cref="IAuthenticationInfo"/>.
        /// </summary>
        /// <param name="info">The actual information.</param>
        void InitializeFrom( IAuthenticationInfo info )
        {
            Throw.CheckNotNullArgument( info );
            UserName = info.User.UserName;
            UserId = info.User.UserId;
            ActualUserName = info.ActualUser.UserName;
            ActualUserId = info.ActualUser.UserId;
            UnsafeUserName = info.UnsafeUser.UserName;
            UnsafeUserId = info.UnsafeUser.UserId;
            IsImpersonated = info.IsImpersonated;
            Level = info.Level;
            Expires = info.Expires;
            CriticalExpires = info.CriticalExpires;
        }
    }
}
