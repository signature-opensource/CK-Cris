using CK.Core;

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
                validator.Warn( $"Culture name '{n}' is unkown. It will be ignored." );
            }
        }

        //[EndpointUbiquitousInfoConfigurator]
        public static void ConfigureCurrentCulture( ICommandWithCurrentCulture cmd, AmbientServiceHub ambientServices )
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


    }
}
