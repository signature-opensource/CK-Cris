using CK.Core;

namespace CK.Cris
{
    public class CrisCultureService : IAutoService
    {
        //[EndpointUbiquitousInfoConfigurator]
        public static void ConfigureCurrentCulture( ICommandWithCurrentCulture cmd, AmbientServiceHub ambientServices )
        {
            if( cmd.CurrentCultureName != null )
            {
                ambientServices.Override( ExtendedCultureInfo.GetExtendedCultureInfo( cmd.CurrentCultureName ) );
            }
        }

    }
}
