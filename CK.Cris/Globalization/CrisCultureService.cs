using CK.Core;
using CK.Cris.AmbientValues;

namespace CK.Cris
{
    /// <summary>
    /// Handles <see cref="ICurrentCulturePart"/>.
    /// </summary>
    public class CrisCultureService : IAutoService
    {
        /// <summary>
        /// Validates that the <see cref="ICurrentCulturePart.CurrentCultureName"/> is not empty
        /// and defined locally. If not, warnings are emitted. 
        /// </summary>
        /// <param name="validator">The message collector.</param>
        /// <param name="part">The part to validate.</param>
        [IncomingValidator]
        public void CheckCultureName( UserMessageCollector validator, ICurrentCulturePart part )
        {
            var n = part.CurrentCultureName;
            if( string.IsNullOrEmpty( n ) || ExtendedCultureInfo.FindExtendedCultureInfo( n ) == null )
            {
                validator.Warn( n == null
                                    ? "Culture name is null. It will be ignored."
                                    : $"Culture name '{n}' is unknown. It will be ignored." );
            }
        }

        /// <summary>
        /// Overrides the <see cref="CurrentCultureInfo"/> with the <see cref="ICurrentCulturePart.CurrentCultureName"/>
        /// if it not empty and locally defined (see <see cref="ExtendedCultureInfo.FindExtendedCultureInfo(string)"/>).
        /// </summary>
        /// <param name="part">The part.</param>
        /// <param name="ambientServices">The ambient service to configure.</param>
        [ConfigureAmbientServices]
        [RestoreAmbientServices]
        public void ConfigureCurrentCulture( ICurrentCulturePart part, AmbientServiceHub ambientServices )
        {
            var n = part.CurrentCultureName;
            if( !string.IsNullOrWhiteSpace( n ) )
            {
                var c = ExtendedCultureInfo.FindExtendedCultureInfo( n );
                if( c != null )
                {
                    ambientServices.Override( c );
                }
            }
        }

        /// <summary>
        /// Handles the <see cref="IAmbientValuesCollectCommand"/> to set the <see cref="ICultureAmbientValues.CurrentCultureName"/>
        /// from the current culture.
        /// </summary>
        /// <param name="cmd">The ambient values collector command.</param>
        /// <param name="currentCulture">The current culture.</param>
        /// <param name="values">The <see cref="IAmbientValues"/> for current culture.</param>
        [CommandPostHandler]
        public void GetCultureAmbientValue( IAmbientValuesCollectCommand cmd, ExtendedCultureInfo currentCulture, ICultureAmbientValues values )
        {
            values.CurrentCultureName = currentCulture.Name;
        }

    }
}
