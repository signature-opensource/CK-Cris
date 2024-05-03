using CK.Core;
using CK.Cris.AmbientValues;

namespace CK.Cris
{
    public class CrisCultureService : IAutoService
    {
        [CommandIncomingValidator]
        public void CheckCultureName( UserMessageCollector validator, ICommandWithCurrentCulture cmd )
        {
            var n = cmd.CurrentCultureName;
            if( string.IsNullOrEmpty( n ) || ExtendedCultureInfo.FindExtendedCultureInfo( n ) == null )
            {
                validator.Warn( $"Culture name '{n}' is unknown. It will be ignored." );
            }
        }

        [ConfigureAmbientServices]
        public void ConfigureCurrentCulture( ICommandWithCurrentCulture cmd, AmbientServiceHub ambientServices )
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
        public virtual void GetCultureAmbientValue( IAmbientValuesCollectCommand cmd, ExtendedCultureInfo c, ICultureAmbientValues values )
        {
            values.CurrentCultureName = c.Name;
        }

    }
}
