using CK.AspNet.Auth;
using CK.Auth;
using CK.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.AspNet.Tests
{

    public sealed class FakeWebFrontLoginService : IWebFrontAuthLoginService
    {
        readonly IAuthenticationTypeSystem _typeSystem;
        readonly FakeUserDatabase _userDB;

        public FakeWebFrontLoginService( IAuthenticationTypeSystem typeSystem, FakeUserDatabase userDB )
        {
            _typeSystem = typeSystem;
            _userDB = userDB;
        }

        // Waiting for .Net...10? (https://github.com/dotnet/runtime/issues/31001):
        // IList<T> should be IReadOnlyList<T>. 
        public IReadOnlyList<IUserInfo> AllUsers => _userDB.AllUsers.ToArray();

        public bool HasBasicLogin => true;

        public IReadOnlyList<string> Providers => new string[] { "Basic" };

        public object CreatePayload( HttpContext ctx, IActivityMonitor monitor, string scheme )
        {
            throw new NotSupportedException();
        }

        public Task<UserLoginResult> BasicLoginAsync( HttpContext ctx, IActivityMonitor monitor, string userName, string password, bool actualLogin )
        {
            IUserInfo? u = null;
            if( password == "success" )
            {
                u = _userDB.AllUsers.FirstOrDefault( i => i.UserName == userName );
                if( u != null && u.Schemes.Any( p => p.Name == "Basic" ) )
                {
                    _userDB.AllUsers.Remove( u );
                    u = _typeSystem.UserInfo.Create( u.UserId, u.UserName, new[] { new StdUserSchemeInfo( "Basic", DateTime.UtcNow ) } );
                    _userDB.AllUsers.Add( u );
                }
                return Task.FromResult( new UserLoginResult( u, 0, null, false ) );
            }
            return Task.FromResult( new UserLoginResult( null, 1, "Login failed!", false ) );
        }

        public Task<UserLoginResult> LoginAsync( HttpContext ctx, IActivityMonitor monitor, string providerName, object payload, bool actualLogin )
        {
            if( providerName != "Basic" ) throw new ArgumentException( "Unknown provider.", nameof( providerName ) );
            var o = payload as List<KeyValuePair<string, object>>;
            if( o == null ) throw new ArgumentException( "Invalid payload." );
            return BasicLoginAsync( ctx, monitor, (string)o.FirstOrDefault( kv => kv.Key == "userName" ).Value, (string)o.FirstOrDefault( kv => kv.Key == "password" ).Value, actualLogin );
        }

        public Task<IAuthenticationInfo> RefreshAuthenticationInfoAsync( HttpContext ctx, IActivityMonitor monitor, IAuthenticationInfo current, DateTime newExpires )
        {
            throw new NotSupportedException( "Not tested." );
        }
    }

}
