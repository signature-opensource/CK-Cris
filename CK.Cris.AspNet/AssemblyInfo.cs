using System;

[assembly: CK.Setup.IsPFeature()]
//[assembly: CK.Setup.RequiredEngine( "CK.Cris.Engine" )]

[assembly: CK.Setup.IsModel()]
[assembly: CK.Setup.RequiredSetupDependency( "CK.Cris.AspNet.Engine" )]

