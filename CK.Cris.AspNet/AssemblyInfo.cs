using System;

[assembly: CK.Setup.IsModel()]
[assembly: CK.Setup.RequiredSetupDependency( "CK.Cris.AspNet.Engine" )]

[assembly: EnsureReference( typeof(CK.Cris.TypeScriptCrisCommandGenerator ))]

[AttributeUsage( AttributeTargets.Assembly )]
class EnsureReferenceAttribute : System.Attribute
{
    public Type Target { get; }

    public EnsureReferenceAttribute( Type target ) => Target = target;
}
