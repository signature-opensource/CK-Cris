using CK.CodeGen;
using CK.Core;
using CK.Cris;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup.Cris;

/// <summary>
/// The [Sensitive] analysis of the Cris types, done at setup: the runtime never uses reflection for it.
/// <list type="bullet">
///     <item>The own sensitive fields of a type: [Sensitive] string properties with a setter (anything else is an error).</item>
///     <item>Its nested Cris fields: properties typed as a Cris Poco (concrete or abstract) that are traversed.</item>
///     <item>
///     Whether it may hold sensitive data: own sensitive fields, or a nested Cris field that may hold a type that may
///     (for an abstract type: any Cris type of the application that implements it). This is a fixed point.
///     </item>
/// </list>
/// </summary>
sealed class CrisSensitiveData
{
    readonly record struct Field( string Name, Type Getter, Type? Setter );

    sealed class Info
    {
        public readonly List<Field> Sensitive = new();
        public readonly List<(Field F, Type FieldType)> Nested = new();
        public bool HasSensitiveData;
    }

    readonly Dictionary<CrisType, Info> _infos;

    CrisSensitiveData( Dictionary<CrisType, Info> infos ) => _infos = infos;

    /// <summary>
    /// Analyzes the Cris types.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="crisTypes">All the Cris types.</param>
    /// <returns>The analysis or null on error.</returns>
    public static CrisSensitiveData? Create( IActivityMonitor monitor, IReadOnlyList<CrisType> crisTypes )
    {
        bool success = true;
        var infos = new Dictionary<CrisType, Info>();
        foreach( var e in crisTypes )
        {
            var info = new Info();
            foreach( var f in e.CrisPocoType.Fields )
            {
                var declared = f.Property.DeclaredProperties;
                var getter = declared[0].PropertyInfo.DeclaringType!;
                if( declared.Any( p => p.PropertyInfo.IsDefined( typeof( SensitiveAttribute ), true ) ) )
                {
                    if( f.Type.Type != typeof( string ) )
                    {
                        monitor.Error( $"[Sensitive] property '{e.PocoName}.{f.Name}' must be a string." );
                        success = false;
                        continue;
                    }
                    var setter = declared.FirstOrDefault( p => p.PropertyInfo.CanWrite )?.PropertyInfo.DeclaringType;
                    if( f.FieldAccess != PocoFieldAccessKind.HasSetter || setter == null )
                    {
                        monitor.Error( $"[Sensitive] property '{e.PocoName}.{f.Name}' must have a setter." );
                        success = false;
                        continue;
                    }
                    info.Sensitive.Add( new Field( f.Name, getter, setter ) );
                }
                else if( typeof( ICrisPoco ).IsAssignableFrom( f.Type.Type ) )
                {
                    info.Nested.Add( (new Field( f.Name, getter, null ), f.Type.Type) );
                }
                else if( f.Type is ICollectionPocoType c && c.ItemTypes.Any( i => typeof( ICrisPoco ).IsAssignableFrom( i.Type ) ) )
                {
                    monitor.Warn( $"'{e.PocoName}.{f.Name}' is a collection of Cris Pocos: their [Sensitive] values are not handled (not redacted, not encrypted)." );
                }
            }
            info.HasSensitiveData = info.Sensitive.Count > 0;
            infos.Add( e, info );
        }
        if( !success ) return null;
        // Fixed point: a nested field may hold a type that may hold sensitive data.
        bool changed;
        do
        {
            changed = false;
            foreach( var (e, info) in infos )
            {
                if( info.HasSensitiveData ) continue;
                if( info.Nested.Any( n => infos.Any( c => c.Value.HasSensitiveData
                                                          && c.Key.CrisPocoType.FamilyInfo.Interfaces.Any( i => n.FieldType.IsAssignableFrom( i.PocoInterface ) ) ) ) )
                {
                    info.HasSensitiveData = changed = true;
                }
            }
        }
        while( changed );
        return new CrisSensitiveData( infos );
    }

    /// <summary>
    /// Gets whether the type may hold sensitive data.
    /// </summary>
    /// <param name="e">The Cris type.</param>
    /// <returns>True if it may hold sensitive data.</returns>
    public bool HasSensitiveData( CrisType e ) => _infos[e].HasSensitiveData;

    /// <summary>
    /// Generates the <c>TransformSensitiveStrings</c> implementation of a model.
    /// </summary>
    /// <param name="e">The Cris type.</param>
    /// <param name="classScope">The model class.</param>
    public void GenerateTransformSensitiveStrings( CrisType e, ITypeScope classScope )
    {
        var info = _infos[e];
        classScope.Append( "public bool TransformSensitiveStrings( CK.Cris.ICrisPoco poco, CK.Cris.SensitiveStringTransformer transformer )" )
                  .OpenBlock();
        if( info.Sensitive.Count > 0 ) classScope.Append( "string? s, n;" ).NewLine();
        foreach( var f in info.Sensitive )
        {
            classScope.Append( "s = ((" ).AppendGlobalTypeName( f.Getter ).Append( ")poco)." ).Append( f.Name ).Append( ";" ).NewLine()
                      .Append( "if( s != null )" ).OpenBlock()
                      .Append( "n = transformer( poco, " ).AppendSourceString( f.Name ).Append( ", s );" ).NewLine()
                      .Append( "if( n == null ) return false;" ).NewLine()
                      .Append( "((" ).AppendGlobalTypeName( f.Setter! ).Append( ")poco)." ).Append( f.Name ).Append( " = n;" ).NewLine()
                      .CloseBlock();
        }
        int i = 0;
        foreach( var (f, _) in info.Nested )
        {
            var v = $"c{i++}";
            classScope.Append( "if( ((" ).AppendGlobalTypeName( f.Getter ).Append( ")poco)." ).Append( f.Name )
                      .Append( " is CK.Cris.ICrisPoco " ).Append( v )
                      .Append( " && !" ).Append( v ).Append( ".CrisPocoModel.TransformSensitiveStrings( " ).Append( v ).Append( ", transformer ) ) return false;" ).NewLine();
        }
        classScope.Append( "return true;" )
                  .CloseBlock();
    }
}
