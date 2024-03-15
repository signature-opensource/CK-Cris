using CK.Core;

namespace CK.Cris.AspNet
{
    public partial class CrisAspNetService
    {
        readonly struct ReadCommandResult
        {
            public readonly IAbstractCommand? Command;
            public readonly IAspNetCrisResult? Error;

            public ReadCommandResult( IAbstractCommand command )
            {
                Throw.CheckNotNullArgument( command );
                Command = command;
                Error = null;
            }

            public ReadCommandResult( IAspNetCrisResult error )
            {
                Throw.CheckNotNullArgument( error );
                Command = null;
                Error = error;
            }

            public void Deconstruct( out IAbstractCommand? c, out IAspNetCrisResult? e )
            {
                c = Command;
                e = Error;
            }
        }

    }
}
