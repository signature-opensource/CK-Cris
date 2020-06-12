using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.Tests
{
    public interface IIntegerAuthorizedCommand : ICommand<int>, IAuthorizationCommandPart
    {
        string Parameter { get; set; }
    }
}
