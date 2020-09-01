using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.StObj.TypeScript;
using CK.StObj.TypeScript.Engine;
using CK.TypeScript.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Setup.Cris
{
    public partial class CommandDirectoryImpl : ITSCodeGenerator
    {
        bool ITSCodeGenerator.ConfigureTypeScriptAttribute( IActivityMonitor monitor, TypeScriptGenerator generator, Type type, TypeScriptAttribute attr, IReadOnlyList<ITSCodeGeneratorType> generatorTypes, ref ITSCodeGenerator? currentHandler )
        {
            bool isPoco = typeof( IPoco ).IsAssignableFrom( type );
            if( isPoco )
            {
                // These very well known IPoco MUST be handled by this since these are the Cris base types.
                bool isCommandOrPart = typeof( ICommand ).IsAssignableFrom( type ) || typeof( ICommandPart ).IsAssignableFrom( type );
                bool isCommandResult = !isCommandOrPart && typeof( ICommandResult ).IsAssignableFrom( type );
                bool isSimpleErrorResult = !(isCommandOrPart && isCommandResult) && typeof( ISimpleErrorResult ).IsAssignableFrom( type );

                if( isCommandOrPart || isCommandResult || isSimpleErrorResult )
                {
                    if( currentHandler != null )
                    {
                        monitor.Warn( $"Type '{type.FullName}' must be handled by CommandDirectoryImpl: replacing current handler '{currentHandler.GetType().FullName}'." );
                    }
                    currentHandler = this;
                }
                else
                {
                    // Another IPoco can be a result of the ICommand<TResult>: it should have been allowed.
                }
            }
            return true;
        }

        bool ITSCodeGenerator.GenerateCode( IActivityMonitor monitor, TypeScriptGenerator g )
        {
            var registry = CommandRegistry.Find( monitor, g.CodeContext );
            Debug.Assert( registry != null, "Implement (CSharp code) has necessarily be successfully called." );

            using( monitor.OpenInfo( $"Generating TypeScript support for {registry.Commands} commands." ) )
            {
                foreach( var cmd in registry.Commands )
                {
                    var result = cmd.PocoResultType;
                    if( result != null ) GenerateRootClass( monitor, g, result.Root );
                    GenerateRootClass( monitor, g, cmd.Command );
                }
            }
            return true;
        }

        bool GenerateRootClass( IActivityMonitor monitor, TypeScriptGenerator g, IPocoRootInfo root )
        {
            bool success = true;
            var className = root.Name;
            if( className.StartsWith( "I" ) && className == root.PrimaryInterface.FullName )
            {
                className = root.Name.Substring( 1 );
            }

            using( monitor.OpenTrace( $"Generating class 'Cris/{className}.ts'." ) )
            {
                var rootFile = g.Context.Root.FindOrCreateFolder( "Cris" ).FindOrCreateFile( className + ".ts" );

                rootFile.Body.Append( "export class " ).Append( className ).Append( " extends" );
                foreach( var i in root.Interfaces )
                {
                    var f = EnsurePocoInterface( monitor, g, i );
                    if( f == null )
                    {
                        success = false;
                        break;
                    }
                    Debug.Assert( f.File != null );
                    rootFile.Imports.EnsureImport( f.TypeName, f.File );
                    rootFile.Body.Append( " " ).Append( f.TypeName );
                }
                rootFile.Body.OpenBlock();
                foreach( var p in root.PropertyList )
                {
                    success &= AppendProperty( monitor, g, rootFile, p );
                }
                rootFile.Body.CloseBlock();
                return success;
            }
        }

        TSTypeFile? EnsurePocoInterface( IActivityMonitor monitor, TypeScriptGenerator g, IPocoInterfaceInfo i )
        {
            var tsTypedFile = g.GetTSTypeFile( monitor, i.PocoFactoryInterface );
            TypeScriptFile? f = tsTypedFile.File;
            if( f == null )
            {
                f = tsTypedFile.EnsureFile();
                f.Body.Append( "export interface " ).Append( tsTypedFile.TypeName );
                bool hasInterface = false;
                foreach( Type baseInterface in i.PocoFactoryInterface.GetInterfaces() )
                {
                    var b = i.Root.Interfaces.FirstOrDefault( p => p.PocoInterface == baseInterface );
                    if( b == null ) continue;
                    if( !hasInterface )
                    {
                        f.Body.Append( " extends " );
                        hasInterface = true;
                    }
                    else f.Body.Append( ", " );
                    var fInterface = EnsurePocoInterface( monitor, g, b );
                    if( fInterface == null ) return null;
                    f.Body.Append( fInterface.TypeName );
                }
                f.Body.OpenBlock();
                bool success = true;
                foreach( var iP in i.PocoInterface.GetProperties() )
                {
                    // Is this interface property implemented at the class level?
                    // If not (ExternallyImplemented property) we currently ignore it.
                    IPocoPropertyInfo? p = i.Root.Properties.GetValueOrDefault( iP.Name );
                    if( p != null )
                    {
                        success &= AppendProperty( monitor, g, f, p );
                    }
                }
                f.Body.CloseBlock();
            }
            return tsTypedFile;
        }

        bool AppendProperty( IActivityMonitor monitor, TypeScriptGenerator g, TypeScriptFile f, IPocoPropertyInfo p )
        {
            bool success = true;
            f.Body.Append( g.ToIdentifier( p.PropertyName ) ).Append( p.IsEventuallyNullable ? "?: " : ": " );
            bool hasUnions = false;
            foreach( var (t, nullInfo) in p.PropertyUnionTypes )
            {
                if( hasUnions ) f.Body.Append( "|" );
                hasUnions = true;
                success &= AppendTypeName( monitor, f.Body, g, t );
            }
            if( !hasUnions )
            {
                success &= AppendTypeName( monitor, f.Body, g, p.PropertyNullableTypeTree, withUndefined: false );
            }
            f.Body.Append( ";" ).NewLine();
            return success;
        }

        bool AppendTypeName( IActivityMonitor monitor, ITSFileBodySection b, TypeScriptGenerator g, NullableTypeTree type, bool withUndefined = true )
        {
            bool success = true;
            var t = type.Type;
            if( t.IsArray )
            {
                b.Append( "Array<" );
                success &= AppendTypeName( monitor, b, g, type.SubTypes[0] );
                b.Append( ">" );
            }
            else if( type.Kind.IsTupleType() )
            {
                b.Append( "[" );
                foreach( var s in type.SubTypes )
                {
                    success &= AppendTypeName( monitor, b, g, s );
                }
                b.Append( "]" );
            }
            else if( t.IsGenericType )
            {
                var tDef = t.GetGenericTypeDefinition();
                if( type.SubTypes.Count == 2 && (tDef == typeof( IDictionary<,> ) || tDef == typeof( Dictionary<,> )) )
                {
                    b.Append( "Map<" );
                    success &= AppendTypeName( monitor, b, g, type.SubTypes[0] );
                    b.Append( "," );
                    success &= AppendTypeName( monitor, b, g, type.SubTypes[1] );
                    b.Append( ">" );
                }
                else if( type.SubTypes.Count == 1 )
                {
                    if( tDef == typeof( ISet<> ) || tDef == typeof( HashSet<> ) )
                    {
                        b.Append( "Set<" );
                        success &= AppendTypeName( monitor, b, g, type.SubTypes[0] );
                        b.Append( ">" );
                    }
                    else if( tDef == typeof( IList<> ) || tDef == typeof( List<> ) )
                    {
                        b.Append( "Array<" );
                        success &= AppendTypeName( monitor, b, g, type.SubTypes[0] );
                        b.Append( ">" );
                    }
                }
                else
                {
                    monitor.Error( $"Unhandled type '{t.FullName}' for TypeScript generation." );
                    return false;
                }
            }
            else if( t == typeof( int ) || t == typeof( float ) || t == typeof( double ) ) b.Append( "number" );
            else if( t == typeof( bool ) ) b.Append( "boolean" );
            else if( t == typeof( string ) ) b.Append( "string" );
            else if( t == typeof( object ) ) b.Append( "object" );
            else
            {
                var other = g.GetTSTypeFile( monitor, t );
                b.File.Imports.EnsureImport( other.TypeName, other.EnsureFile() );
                b.Append( other.TypeName );
            }
            if( withUndefined && type.Kind.IsNullable() ) b.Append( "|undefined" );
            return success;
        }

        bool AppendTypeName( IActivityMonitor monitor, ITSFileBodySection b, TypeScriptGenerator g, Type t )
        {
            bool success = true;
            if( t.IsArray )
            {
                b.Append( "Array<" );
                success &= AppendTypeName( monitor, b, g, t.GetElementType()! );
                b.Append( ">" );
            }
            else if( t.IsValueTuple() )
            {
                b.Append( "[" );
                foreach( var s in t.GetGenericArguments() )
                {
                    success &= AppendTypeName( monitor, b, g, s );
                }
                b.Append( "]" );
            }
            else if( t.IsGenericType )
            {
                var tDef = t.GetGenericTypeDefinition();
                if( tDef == typeof( IDictionary<,> ) || tDef == typeof( Dictionary<,> ) )
                {
                    var args = t.GetGenericArguments();
                    b.Append( "Map<" );
                    success &= AppendTypeName( monitor, b, g, args[0] );
                    b.Append( "," );
                    success &= AppendTypeName( monitor, b, g, args[1] );
                    b.Append( ">" );
                }
                else if( tDef == typeof( ISet<> ) || tDef == typeof( HashSet<> ) )
                {
                    b.Append( "Set<" );
                    success &= AppendTypeName( monitor, b, g, t.GetGenericArguments()[0] );
                    b.Append( ">" );
                }
                else if( tDef == typeof( IList<> ) || tDef == typeof( List<> ) )
                {
                    b.Append( "Array<" );
                    success &= AppendTypeName( monitor, b, g, t.GetGenericArguments()[0] );
                    b.Append( ">" );
                }
                else
                {
                    monitor.Error( $"Unhandled type '{t.FullName}' for TypeScript generation." );
                    return false;
                }
            }
            else if( t == typeof( int ) || t == typeof( float ) || t == typeof( double ) ) b.Append( "number" );
            else if( t == typeof( bool ) ) b.Append( "boolean" );
            else if( t == typeof( string ) ) b.Append( "string" );
            else if( t == typeof( object ) ) b.Append( "unknown" );
            else
            {
                var other = g.GetTSTypeFile( monitor, t );
                b.File.Imports.EnsureImport( other.TypeName, other.EnsureFile() );
                b.Append( other.TypeName );
            }
            return success;
        }


    }
}
