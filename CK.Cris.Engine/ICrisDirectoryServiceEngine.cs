using System.Collections.Generic;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Exposes the Cris objects.
    /// </summary>
    public interface ICrisDirectoryServiceEngine
    {
        /// <summary>
        /// Gets the Poco type system.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets all the discovered commands and events ordered by their <see cref="CrisType.CrisPocoIndex"/>.
        /// </summary>
        IReadOnlyList<CrisType> CrisTypes { get; }

        /// <summary>
        /// Finds a command or event entry from its Poco definition.
        /// </summary>
        /// <param name="poco">The poco definition.</param>
        /// <returns>The entry or null.</returns>
        CrisType? Find( IPrimaryPocoType poco );
    }

}
