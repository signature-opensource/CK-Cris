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
        /// Gets a non null abstract CrisPocoType only if at least one concrete event
        /// or command exists.
        /// </summary>
        IAbstractPocoType CrisPocoType { get; }

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

        /// <summary>
        /// Gets whether a field (of a command) is an [UbiquitousValue].
        /// </summary>
        /// <param name="field">The field to test.</param>
        /// <returns>True if the field is an ubiquitous value.</returns>
        bool IsUbiquitousValueField( IPrimaryPocoField field );
    }

}
