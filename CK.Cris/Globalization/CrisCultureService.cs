using CK.Core;
using CK.Cris.AmbientValues;

namespace CK.Cris
{
    public class CrisCultureService : IAutoService
    {
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

        [ConfigureAmbientServices]
        [RestoreAmbientServices]
        public void ConfigureCurrentCulture( ICommandCurrentCulture vs. ICurrentCulturePart cmd, AmbientServiceHub ambientServices )
        {
            var n = cmd.CurrentCultureName;
            if( !string.IsNullOrWhiteSpace( n ) )
            {
                var c = ExtendedCultureInfo.FindExtendedCultureInfo( n );
                if( c != null )
                {
                    ambientServices.Override( c );
                }
            }
        }

        [CommandPostHandler]
        public void GetCultureAmbientValue( IAmbientValuesCollectCommand cmd, ExtendedCultureInfo c, ICultureAmbientValues values )
        {
            values.CurrentCultureName = c.Name;
        }

    }
}
