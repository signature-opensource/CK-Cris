using CK.Auth;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.Tests
{
    public sealed class FakeUserDatabase : IUserInfoProvider
    {
        readonly List<IUserInfo> _users;
        readonly IAuthenticationTypeSystem _typeSystem;

        public FakeUserDatabase( IAuthenticationTypeSystem typeSystem )
        {
            _users = new List<IUserInfo>
            {
                // Albert is registered in Basic.
                typeSystem.UserInfo.Create( 1, "System" ),
                typeSystem.UserInfo.Create( 3712, "Albert", new[] { new StdUserSchemeInfo( "Basic", DateTime.MinValue ) } ),
                typeSystem.UserInfo.Create( 3713, "Robert" ),
                // Hubert is registered in Google.
                typeSystem.UserInfo.Create( 3714, "Hubert", new[] { new StdUserSchemeInfo( "Google", DateTime.MinValue ) } )
            };
            _typeSystem = typeSystem;
        }

        public IList<IUserInfo> AllUsers => _users;

        public ValueTask<IUserInfo> GetUserInfoAsync( IActivityMonitor monitor, int userId )
        {
            var u = _users.FirstOrDefault( u => u.UserId == userId ) ?? _typeSystem.UserInfo.Anonymous;
            return ValueTask.FromResult( u );
        }
    }

}
