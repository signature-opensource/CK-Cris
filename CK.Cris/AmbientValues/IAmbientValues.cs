using CK.Core;

namespace CK.Cris.AmbientValues
{
    /// <summary>
    /// Defines an extensible set of properties that can be initialized from or need to
    /// be challenged against the ubiquitous services.
    /// <para>
    /// The <see cref="IAmbientValuesCollectCommand"/> sent to the endpoint returns these values.
    /// </para>
    /// </summary>
    public interface IAmbientValues : IPoco
    {
    }
}
