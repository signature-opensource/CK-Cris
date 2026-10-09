using CK.Core;
using CK.Cris;
using CK.IO.Authentication;

namespace CK.Auth;

/// <summary>
/// Limits the <see cref="ICommandLoginUserData.UserData"/> of the login commands: at most <see cref="MaxEntries"/> entries
/// and <see cref="MaxLength"/> characters in total (keys and values).
/// <para>
/// The user data come from anonymous clients and a remote login keeps them in memory until its callback: unbounded data
/// would be a memory exhaustion vector.
/// </para>
/// </summary>
public sealed class LoginUserDataValidator : ISingletonAutoService
{
    /// <summary>
    /// The maximal number of entries: 16.
    /// </summary>
    public const int MaxEntries = 16;

    /// <summary>
    /// The maximal total length of the keys and the values, in characters: 4096.
    /// </summary>
    public const int MaxLength = 4096;

    /// <summary>
    /// Checks the limits of the <see cref="ICommandLoginUserData.UserData"/>.
    /// </summary>
    /// <param name="c">The message collector.</param>
    /// <param name="cmd">The login command.</param>
    [IncomingValidator]
    public void ValidateLoginUserData( UserMessageCollector c, ICommandLoginUserData cmd )
    {
        var data = cmd.UserData;
        if( data.Count > MaxEntries )
        {
            c.Error( $"Too many login user data: at most {MaxEntries} entries are allowed.", "Authentication.UserDataTooLarge" );
            return;
        }
        int length = 0;
        foreach( var (key, value) in data )
        {
            length += key.Length + (value?.Length ?? 0);
        }
        if( length > MaxLength )
        {
            c.Error( $"The login user data are too large: at most {MaxLength} characters are allowed (keys and values).", "Authentication.UserDataTooLarge" );
        }
    }
}
