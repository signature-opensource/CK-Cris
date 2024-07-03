using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CK.Auth
{
    /// <summary>
    /// Basic authentication command. 
    /// </summary>
    public interface IBasicLoginCommand : ICommand<IAuthenticationResult>
    {
        /// <summary>
        /// Gets or sets the user name.
        /// </summary>
        string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        string Password { get; set; }

        /// <summary>
        /// Gets or sets whether on success, the user must impersonate
        /// the currently logged in user (if any).
        /// </summary>
        bool ImpersonateActualUser { get; set; }

        /// <summary>
        /// Gets or sets an optional authentication lifetime. By default, the server
        /// uses its default. Note that there is no guaranty that this duration
        /// will be applied (the server can restrict this).
        /// </summary>
        TimeSpan? ExpiresTimeSpan { get; set; }

        /// <summary>
        /// Gets or sets an optional critical authentication lifetime.
        /// By default, the server uses its default. Note that there is no guaranty that this duration
        /// will be applied (the server can restrict this).
        /// </summary>
        TimeSpan? CriticalExpiresTimeSpan { get; set; }
    }
}
