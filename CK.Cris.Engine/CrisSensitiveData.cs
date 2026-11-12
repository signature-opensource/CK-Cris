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
///     <item>
///     Its nested Cris fields, that are traversed: properties typed as a Cris Poco (concrete or abstract), and properties
///     that reach Cris Pocos only through lists, sets, arrays, dictionary values and anonymous records (like the
///     <c>Events</c> of an executed command, or a list of <c>(IAbstractCommand Command, string? Description)</c>).
///     </item>
///     <item>
///     Whether it may hold sensitive data: own sensitive fields, or a nested Cris field that may hold a type that may
///     (for an abstract type: any Cris type of the application that implements it). This is a fixed point.
///     </item>
///     <item>
///     Any other field (a collection of plain IPocos, a plain IPoco, a named record, a union type) that may hold a [Sensitive] value is an error:
///     these values are not handled (they would be logged, and would cross a party boundary unprotected). A field typed
///     as <c>object</c> or as an abstract plain IPoco is not analyzed.
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
        // The fields that reach Cris Pocos through lists, sets, arrays, dictionary values and anonymous records:
        // they are traversed at runtime (CrisTypes are the reachable Cris Poco types).
        public readonly List<(Field F, List<Type> CrisTypes)> NestedCollections = new();
        // The fields that are neither sensitive nor nested Cris Pocos: they must not hold sensitive values.
        public readonly List<IPocoField> Others = new();
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
        List<Type> reachable;
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
                else if( IsTraversable( f.Type, reachable = new List<Type>() ) )
                {
                    info.NestedCollections.Add( (new Field( f.Name, getter, null ), reachable) );
                }
                else
                {
                    info.Others.Add( f );
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
                if( info.Nested.Select( n => n.FieldType ).Concat( info.NestedCollections.SelectMany( n => n.CrisTypes ) )
                        .Any( t => CrisTypeMayHold( infos, t ) ) )
                {
                    info.HasSensitiveData = changed = true;
                }
            }
        }
        while( changed );
        // The sensitive values that cannot be reached are errors.
        foreach( var (e, info) in infos )
        {
            foreach( var f in info.Others )
            {
                if( MayHoldUnhandledSensitive( infos, f.Type, new HashSet<IPocoType>() ) )
                {
                    monitor.Error( $"'{e.PocoName}.{f.Name}' may hold [Sensitive] values that are not handled (not redacted, not encrypted): " +
                                   $"only the properties of a Cris Poco and of its nested Cris Pocos (a property typed as a Cris Poco) are handled." );
                    success = false;
                }
            }
        }
        return success ? new CrisSensitiveData( infos ) : null;
    }

    // A field that reaches Cris Pocos only through lists, sets, arrays, dictionary values (keys that reach nothing)
    // and anonymous records (value tuples: their other fields reach nothing); its Cris Poco types are collected.
    static bool IsTraversable( IPocoType t, List<Type> reachable )
    {
        t = t.NonNullable;
        if( typeof( ICrisPoco ).IsAssignableFrom( t.Type ) )
        {
            reachable.Add( t.Type );
            return true;
        }
        switch( t )
        {
            case ICollectionPocoType c when c.Kind is PocoTypeKind.List or PocoTypeKind.HashSet or PocoTypeKind.Array:
                return IsTraversable( c.ItemTypes[0], reachable );
            case ICollectionPocoType c when c.Kind is PocoTypeKind.Dictionary:
                return !MayHoldUnhandledSensitive( null, c.ItemTypes[0], new HashSet<IPocoType>() )
                       && IsTraversable( c.ItemTypes[1], reachable );
            case IRecordPocoType r when t.Kind is PocoTypeKind.AnonymousRecord:
                bool any = false;
                foreach( var f in r.Fields )
                {
                    if( f.Originator is System.Reflection.MemberInfo m && m.IsDefined( typeof( SensitiveAttribute ), true ) ) return false;
                    if( IsTraversable( f.Type, reachable ) ) any = true;
                    else if( MayHoldUnhandledSensitive( null, f.Type, new HashSet<IPocoType>() ) ) return false;
                }
                return any;
        }
        return false;
    }

    static bool CrisTypeMayHold( Dictionary<CrisType, Info> infos, Type t )
    {
        return infos.Any( c => c.Value.HasSensitiveData
                               && c.Key.CrisPocoType.FamilyInfo.Interfaces.Any( i => t.IsAssignableFrom( i.PocoInterface ) ) );
    }

    // A reachability: true if a reachable type declares a [Sensitive] member (each type is visited once).
    // Without infos (before the fixed point), any Cris Poco counts.
    static bool MayHoldUnhandledSensitive( Dictionary<CrisType, Info>? infos, IPocoType t, HashSet<IPocoType> visited )
    {
        t = t.NonNullable;
        if( typeof( ICrisPoco ).IsAssignableFrom( t.Type ) ) return infos == null || CrisTypeMayHold( infos, t.Type );
        if( !visited.Add( t ) ) return false;
        return t switch
        {
            ICollectionPocoType c => c.ItemTypes.Any( i => MayHoldUnhandledSensitive( infos, i, visited ) ),
            IPrimaryPocoType p => p.Fields.Any( f => f.Property.DeclaredProperties.Any( d => d.PropertyInfo.IsDefined( typeof( SensitiveAttribute ), true ) )
                                                     || MayHoldUnhandledSensitive( infos, f.Type, visited ) ),
            ISecondaryPocoType sec => MayHoldUnhandledSensitive( infos, sec.PrimaryPocoType, visited ),
            IRecordPocoType r => r.Fields.Any( f => (f.Originator is System.Reflection.MemberInfo m && m.IsDefined( typeof( SensitiveAttribute ), true ))
                                                    || MayHoldUnhandledSensitive( infos, f.Type, visited ) ),
            // A union type: one of its types (object and the abstract IPoco are not analyzed).
            IOneOfPocoType u when t.Kind == PocoTypeKind.UnionType => u.AllowedTypes.Any( a => MayHoldUnhandledSensitive( infos, a, visited ) ),
            _ => false
        };
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
        foreach( var (f, _) in info.NestedCollections )
        {
            classScope.Append( "if( !TransformNested( ((" ).AppendGlobalTypeName( f.Getter ).Append( ")poco)." ).Append( f.Name )
                      .Append( ", transformer ) ) return false;" ).NewLine();
        }
        classScope.Append( "return true;" )
                  .CloseBlock();
        if( info.NestedCollections.Count > 0 )
        {
            // The values of a traversable field (see IsTraversable): Cris Pocos, collections, dictionaries and value tuples.
            classScope.Append( "static bool TransformNested( object? o, CK.Cris.SensitiveStringTransformer transformer )" )
                      .OpenBlock()
                      .Append( "switch( o )" ).OpenBlock()
                      .Append( "case null: case string: return true;" ).NewLine()
                      .Append( "case CK.Cris.ICrisPoco c: return c.CrisPocoModel.TransformSensitiveStrings( c, transformer );" ).NewLine()
                      .Append( "case System.Collections.IDictionary d: foreach( var v in d.Values ) if( !TransformNested( v, transformer ) ) return false; return true;" ).NewLine()
                      .Append( "case System.Runtime.CompilerServices.ITuple t: for( int i = 0; i < t.Length; ++i ) if( !TransformNested( t[i], transformer ) ) return false; return true;" ).NewLine()
                      .Append( "case System.Collections.IEnumerable e: foreach( var v in e ) if( !TransformNested( v, transformer ) ) return false; return true;" ).NewLine()
                      .Append( "default: return true;" ).NewLine()
                      .CloseBlock()
                      .CloseBlock();
        }
    }
}
