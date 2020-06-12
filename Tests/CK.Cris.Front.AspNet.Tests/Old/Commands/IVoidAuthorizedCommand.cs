using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.Tests
{
    public interface IVoidAuthorizedCommand : ICommand, IAuthorizationCommandPart
    {
        string Parameter { get; set; }
    }
}
