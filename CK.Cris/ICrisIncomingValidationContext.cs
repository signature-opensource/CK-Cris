using CK.Core;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// This interface can appear in [IncomingValidator] methods where it
    /// can replace the <see cref="UserMessageCollector"/> since the <see cref="Messages"/> collector
    /// is exposed here.
    /// <para>
    /// This interface enables composite commands or events validation. Generally all subordinate commands (or events)
    /// must be valid for a a command (or event) to be valid.
    /// </para>
    /// </summary>
    public interface ICrisIncomingValidationContext
    {
        /// <summary>
        /// Gets the validation messages collector.
        /// </summary>
        UserMessageCollector Messages { get; }

        /// <summary>
        /// Validates a command or event.
        /// </summary>
        /// <param name="crisPoco">The command or event to validate.</param>
        /// <returns>The awaitable.</returns>
        ValueTask ValidateAsync( ICrisPoco crisPoco );
    }
}
