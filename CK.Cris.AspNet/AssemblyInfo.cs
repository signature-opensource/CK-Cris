using System;

[assembly: CK.Setup.IsModel()]
[assembly: CK.Setup.RequiredSetupDependency( "CK.Cris.AspNet.Engine" )]

[assembly: PreserveAssemblyReference( typeof( CK.Cris.TypeScriptCrisCommandGenerator ))]
