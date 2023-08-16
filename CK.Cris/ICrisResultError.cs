using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Cris
{
    /// <summary>
    /// Simple model for errors: a list of <see cref="ResultMessage"/>.
    /// Since this is a <see cref="IPoco"/>, it can easily be extended.
    /// <para>
    /// You can use the helper <see cref="PocoFactoryExtensions.Create(IPocoFactory{ICrisResultError}, string, string?[])"/> extension
    /// method to create a error from error messages.
    /// </para>
    /// </summary>
    [ExternalName( "CrisResultError" )]
    public interface ICrisResultError : IPoco
    {
        /// <summary>
        /// Gets the list of user messages.
        /// At least one of them should be a <see cref="ResultMessageLevel.Error"/> but this is not checked.
        /// </summary>
        List<ResultMessage> UserMessages { get; }

        /// <summary>
        /// Gets the list of error strings.
        /// </summary>
        public IEnumerable<string> Errors => UserMessages.Where( m => m.Level == ResultMessageLevel.Error ).Select( m => m.Text );
    }
}
