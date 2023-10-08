using CK.Core;

namespace CK.Cris
{
    public class CrisCultureService : IAutoService
    {
        //[EndpointUbiquitousInfoConfigurator]
        public static void ConfigureCurrentCulture( ICommandWithCurrentCulture cmd, EndpointUbiquitousInfo ubiquitousInfo )
        {
            if( cmd.CurrentCultureName != null )
            {
                ubiquitousInfo.Override( ExtendedCultureInfo.GetExtendedCultureInfo( cmd.CurrentCultureName ) );
            }
        }

    }
}
