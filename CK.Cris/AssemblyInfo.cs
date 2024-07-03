
[assembly: CK.Core.PreserveAssemblyReference( typeof( CK.Core.NormalizedCultureInfo ) )]
[assembly: CK.Setup.IsPFeature()]
//[assembly: CK.Setup.RequiredEngine( "CK.Cris.Engine" )]

[assembly: CK.Setup.IsModel()]
[assembly: CK.Setup.RequiredSetupDependency( "CK.Cris.Engine" )]

