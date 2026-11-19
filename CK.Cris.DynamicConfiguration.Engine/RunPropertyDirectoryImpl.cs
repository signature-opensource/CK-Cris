using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// Code generator of the <see cref="CK.Cris.DynamicConfiguration.RunPropertyDirectory"/>: the run properties of the types
/// of the App that declare a section (<see cref="ConfigurationSectionAttribute"/>, <see cref="ConfigurationRunProperty"/>),
/// with typed accessors from the root services (no reflection at run time). The ones that the configuration restricts
/// below Run (<see cref="BinPathConfiguration.LockedRunValues"/>) are initialized, not changed.
/// </summary>
public sealed class RunPropertyDirectoryImpl : CSCodeGeneratorType
{
    /// <inheritdoc />
    public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
    {
        var group = c.CurrentRun.ConfigurationGroup;
        IReadOnlySet<string> locked = group.IsUnifiedPure ? new HashSet<string>() : group.Configuration.LockedRunValues;
        var properties = new List<ConfigurationRunProperty>();
        bool success = true;
        foreach( var t in group.TypeSet.OrderBy( t => t.CSharpName, StringComparer.Ordinal ) )
        {
            if( ConfigurationRunProperty.GetSection( t.Type ) == null ) continue;
            if( !typeof( ISingletonAutoService ).IsAssignableFrom( t.Type ) && !typeof( IRealObject ).IsAssignableFrom( t.Type ) )
            {
                monitor.Error( $"'{t.CSharpName}' declares a configuration section: it must be an App singleton (an ISingletonAutoService or a real object)." );
                success = false;
                continue;
            }
            if( !ConfigurationRunProperty.TryCollect( monitor, t.Type, out var p ) ) success = false;
            else properties.AddRange( p );
        }
        if( !success ) return CSCodeGenerationResult.Failed;
        scope.Append( "static readonly global::CK.Cris.DynamicConfiguration.RunProperty[] _properties = new global::CK.Cris.DynamicConfiguration.RunProperty[] {" ).NewLine();
        foreach( var p in properties.OrderBy( p => p.Path, StringComparer.OrdinalIgnoreCase ) )
        {
            var owner = $"(({p.Owner.ToGlobalTypeName()})s.GetService( typeof( {p.Owner.ToGlobalTypeName()} ) )!)";
            var access = owner + string.Concat( p.Chain.Select( x => "." + x.Name ) );
            scope.Append( "new global::CK.Cris.DynamicConfiguration.RunProperty( " ).AppendSourceString( p.Path )
                 .Append( ", typeof( " ).Append( p.ValueType.ToGlobalTypeName() ).Append( " ), " )
                 .Append( locked.Contains( p.Path ) ? "false" : "true" ).Append( "," ).NewLine()
                 .Append( "    s => " ).Append( access ).Append( "," ).NewLine()
                 .Append( "    ( s, v ) => " ).Append( access ).Append( " = (" ).Append( p.ValueType.ToGlobalTypeName() ).Append( ")v! )," ).NewLine();
        }
        scope.Append( "};" ).NewLine()
             .Append( "public override global::System.Collections.Generic.IReadOnlyList<global::CK.Cris.DynamicConfiguration.RunProperty> Properties => _properties;" ).NewLine();
        if( properties.Count > 0 )
        {
            monitor.Info( $"{properties.Count} run propert{(properties.Count > 1 ? "ies" : "y")}: '{properties.Select( p => locked.Contains( p.Path ) ? p.Path + " (restricted)" : p.Path ).Concatenate( "', '" )}'." );
        }
        return CSCodeGenerationResult.Success;
    }
}
