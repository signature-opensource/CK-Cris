using CK.Auth;
using CK.Core;
using CK.Cris;
using CK.Setup;
using System.Threading.Tasks;

namespace CK.AspNet.Auth.Cris
{
    [EndpointSingletonService]
    public class CrisWebFrontAuthCommandHandler : ISingletonAutoService
    {
        readonly WebFrontAuthService _authService;

        public CrisWebFrontAuthCommandHandler( WebFrontAuthService authService )
        {
            _authService = authService;
        }

        [CommandHandler]
        public async Task<IAuthenticationResult> BasicLoginAsync( IActivityMonitor monitor,
                                                                  ScopedHttpContext httpContext,
                                                                  CurrentCultureInfo culture,
                                                                  IBasicLoginCommand cmd )
        {
            var r = await _authService.BasicLoginCommandAsync( monitor,
                                                               httpContext.HttpContext,
                                                               cmd.UserName,
                                                               cmd.Password,
                                                               cmd.ExpiresTimeSpan,
                                                               cmd.CriticalExpiresTimeSpan,
                                                               cmd.ImpersonateActualUser );
            var result = cmd.CreateResult();
            if( r.Success )
            {
                result.Info.InitializeFrom( r.Info );
                result.Token = r.Token;
            }
            else
            {
                result.UserMessages.Add( UserMessage.Create( culture, UserMessageLevel.Error, r.ErrorId ) );
                if( !string.IsNullOrEmpty( r.ErrorText ) )
                {
                    result.UserMessages.Add( UserMessage.Create( culture, UserMessageLevel.Error, r.ErrorText ) );
                }
            }
            return result;
        }

        [CommandHandler]
        public async Task<IAuthenticationResult> RefreshAsync( IActivityMonitor monitor,
                                                               ScopedHttpContext httpContext,
                                                               IRefreshAuthenticationCommand cmd )
        {
            var (info, token) = await _authService.RefreshCommandAsync( monitor,
                                                            httpContext.HttpContext,
                                                            cmd.CallBackend );
            return cmd.CreateResult( result =>
            {
                result.Info.InitializeFrom( info );
                result.Token = token;
            } );
        }

        [CommandHandler]
        public Task LogoutAsync( IActivityMonitor monitor,
                                       ScopedHttpContext httpContext,
                                       ILogoutCommand cmd )
        {
            return _authService.LogoutCommandAsync( monitor, httpContext.HttpContext );
        }

    }
}
