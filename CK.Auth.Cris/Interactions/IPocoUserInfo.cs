using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Auth
{
    /// <summary>
    /// Poco of <see cref="IUserInfo"/>.
    /// </summary>
    public interface IPocoUserInfo : IPoco
    {
        /// <summary>
        /// See <see cref="IUserInfo.UserName"/>.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// See <see cref="IUserInfo.UserId"/>.
        /// </summary>
        int UserId { get; set; }

        /// <summary>
        /// See <see cref="IUserInfo.Schemes"/>.
        /// </summary>
        List<(string Scheme, DateTime LastUsed)> Schemes { get; }

        /// <summary>
        /// Initializes this Poco from a <see cref="IUserInfo"/>.
        /// </summary>
        /// <param name="info">The actual information.</param>
        void InitializeFrom( IUserInfo info )
        {
            UserName = info.UserName;
            UserId = info.UserId;
            Schemes.Clear();
            Schemes.AddRange( info.Schemes.Select( s => (s.Name,s.LastUsed) ) );
        }
    }
}
