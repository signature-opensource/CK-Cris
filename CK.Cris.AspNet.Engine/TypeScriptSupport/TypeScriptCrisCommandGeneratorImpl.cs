using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Cris.AspNet;
using CK.Setup.Cris;
using CK.StObj.TypeScript;
using CK.StObj.TypeScript.Engine;
using CK.TypeScript.CodeGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl : ITSCodeGenerator
    {
        CrisRegistry? _registry;

        bool ITSCodeGenerator.ConfigureTypeScriptAttribute( IActivityMonitor monitor, ITSTypeFileBuilder builder, TypeScriptAttribute a )
        {
            // Nothing to do: we don't want to interfere with the standard IPoco handling.
            return true;
        }

        bool ITSCodeGenerator.Initialize( IActivityMonitor monitor, TypeScriptContext context )
        {
            _registry = CrisRegistry.Find( monitor, context.CodeContext );
            Throw.DebugAssert( _registry != null, "CSharp code implementation has necessarily been successfully called." );
            context.PocoCodeGenerator.PocoGenerating += OnPocoGenerating;
            return true;
        }

        bool ITSCodeGenerator.GenerateCode( IActivityMonitor monitor, TypeScriptContext g )
        {
            // Nothing to do here. We just react to the OnPocoGenerating event raised by the
            // Poco generator and handles ICrisPoco that MUST be declared as a TypeScript type
            // (either by the TSTypeScriptAttribute ot by the configuration).
            return true;
        }

        // We capture the list of the Ambient Values properties.
        IReadOnlyList<TypeScriptPocoPropertyInfo>? _ambientValuesProperties;

        /// <summary>
        /// This is where everything happens: if the Poco being generated is a ICommand, then:
        /// <list type="bullet">
        /// <item>
        /// We add the commandModel signature to all the IPoco interfaces and its implementation
        /// on the Poco implementation. It is the CommandModel&lt;TResult&gt; that carries the
        /// command result type (if any) so that type inference can be used to type the return
        /// of the sendAsync method.
        /// </item>
        /// <item>
        /// Poco properties that with same ambient values names are checked (their type must be assignable
        /// to the ambient value's type), their corresponding parameter is removed from the create factory
        /// method and the applyAmbientValues method (on the commandModel) applies them.
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">The <see cref="TSIPocoCodeGenerator"/>.</param>
        /// <param name="e">The Poco generation information.</param>
        void OnPocoGenerating( object? sender, PocoGeneratingEventArgs e )
        {
            Throw.DebugAssert( _registry != null, "CS code generation ran. Implement (CSharp code) has necessarily been successfully called." );

            if( typeof(ICrisPoco).IsAssignableFrom( e.TypeFile.Type ) )
            {
                var cmd = _registry.Find( e.PocoClass.PocoRootInfo );
                // A IPoco that supports ICommand should be a registered command.
                // If it's not the case, this is weird but that MAY be possible.
                // Defensive programming here.
                if( cmd == null ) return;

                // Force the IAmbientValues to be registered first.
                if( e.EnsurePoco( _registry.AmbientValues ) == null )
                {
                    e.SetError( $"Since generating the IAmbientValues TypeScript failed, no command can be generated." );
                    return;
                }

                Throw.DebugAssert( _ambientValuesProperties != null, "The PocoGeneratingEventArgs for IAmbientValues has been called." );

                EnsureCrisModel( e );

                // Declares the command result type, whatever it is.
                if( cmd.ResultType != typeof( void ) )
                {
                    e.TypeFile.Context.DeclareTSType( e.Monitor, cmd.ResultType );
                }

                // Compute the (potentially) type name only once by caching the signature.
                var b = e.PocoClass.Part;

                // The commandModel is a getter that returns a static (shared commandModel instance).
                b.Append( "get " ).Append( e.TypeFile.Context.PascalCase ? "C" : "c" ).Append( "ommandModel() { return " )
                 .Append( e.PocoClass.TypeName ).Append( "._commandModel; }" ).NewLine();
                //
                b.Append( "private static readonly _commandModel: CommandModel<" );
                var resultTypeName = b.AppendAndGetComplexTypeName( e.Monitor, e.TypeFile.Context, cmd.ResultNullableTypeTree );
                if( resultTypeName == null )
                {
                    e.SetError();
                    return;
                }
                b.Append( "> = " )
                    .OpenBlock()
                        .Append( "commandName: " ).AppendSourceString( cmd.PocoName ).Append( "," ).NewLine()
                        .Append( "applyAmbientValues( command: any, a: any, o: any )" )
                        .OpenBlock()
                            .Append( ApplyAmbientValues )
                        .CloseBlock()
                    .CloseBlock( withSemiColon: true );

                void ApplyAmbientValues( ITSCodePart b )
                {
                    Throw.DebugAssert( _ambientValuesProperties != null );
                    bool atLeastOne = false;
                    foreach( var a in _ambientValuesProperties )
                    {
                        // Find the property with the same (parameter) name.
                        var fromAmbient = e.PocoClass.Properties.FirstOrDefault( p => p.Property.Name == a.Property.Name );
                        if( fromAmbient != null )
                        {
                            // Documents it.
                            fromAmbient.Property.Comment += Environment.NewLine + "(This is an Ambient Value.)";
                            e.Monitor.Debug( $"Property '{fromAmbient.Property.Name}' is an ambient value." );
                            // Remove the parameter.
                            fromAmbient.CreateMethodParameter = null;
                            // Adds the assignment: this property comes from its ambient value.
                            if( atLeastOne ) b.NewLine();
                            if( fromAmbient.PocoProperty.IsReadOnly )
                            {
                                throw new NotImplementedException( "Read only properties ambient value assignment." );
                            }
                            else
                            {
                                // Generates:
                                // if( command.color === undefined ) command.color = o.color !== null ? o.color : a.color;
                                b.Append( "if( command." ).Append( fromAmbient.Property.Name ).Append( " === undefined ) command." )
                                 .Append( fromAmbient.Property.Name )
                                 .Append( " = o." ).Append( fromAmbient.Property.Name ).Append(" !== null ? o.")
                                 .Append( fromAmbient.Property.Name ).Append( " : a." ).Append( fromAmbient.Property.Name ).Append( ";" ).NewLine();
                            }
                            atLeastOne = true;
                        }
                    }
                    if( !atLeastOne && e.PocoClass.PocoRootInfo != _registry.AmbientValues ) b.Append( "// This command has no property that appear in the Ambient Values." ).NewLine();
                }
            }
            else if( e.PocoClass.PocoRootInfo == _registry.AmbientValues )
            {
                Throw.DebugAssert( _ambientValuesProperties == null );
                _ambientValuesProperties = e.PocoClass.Properties;
                var context = e.TypeFile.Context;
                var fOverride = context.Root.FindOrCreateFile( e.TypeFile.Folder.AppendPart( "AmbientValuesOverride.ts" ) );
                var b = fOverride.Body.CreatePart();
                b.Append( """
                    /**
                    * To manage ambient values overrides, we use the null value to NOT override:
                    *  - We decided to map C# null to undefined because working with both null
                    *    and undefined is difficult.
                    *  - Here, the null is used, so that undefined can be used to override with an undefined that will
                    *    be a null value on the C# side.
                    * All the properties are initialized to null in the constructor.
                    **/

                    """ )
                 .Append( "export class AmbientValuesOverride" )
                 .OpenBlock()
                 .CreatePart( out var propertiesPart )
                 .Append( "constructor()" )
                     .OpenBlock()
                     .CreatePart( out var ctorPart )
                     .CloseBlock()
                 .CloseBlock();

                GenerateProperties( propertiesPart, ctorPart, _ambientValuesProperties );
            }

            void GenerateProperties( ITSCodePart propertiesPart,
                                     ITSCodePart ctorPart,
                                     IReadOnlyList<TypeScriptPocoPropertyInfo> ambientValuesProperties )
            {
                foreach( var property in ambientValuesProperties )
                {
                    propertiesPart.Append( property.Property.Name ).Append( ": " )
                                  .Append( property.Property.Type );
                    if( property.Property.Optional )
                    {
                        propertiesPart.Append( "|undefined" ).NewLine();
                    }
                    propertiesPart.Append( "|null;" ).NewLine();
                    ctorPart.Append( "this." ).Append( property.Property.Name ).Append( " = null;" ).NewLine();
                }
            }
        }

        static void EnsureCrisModel( PocoGeneratingEventArgs e )
        {
            var folder = e.TypeFile.Context.Root.FindOrCreateFolder( "CK/Cris" );
            var fModel = folder.FindOrCreateFile( "Model.ts", out bool created );
            if( created )
            {
                GenerateCrisModelFile( e.Monitor, fModel );
                GenerateCrisEndpoint( e.Monitor, folder.FindOrCreateFile( "CrisEndpoint.ts" ) );
                GenerateCrisHttpEndpoint( e.Monitor, folder.FindOrCreateFile( "HttpCrisEndpoint.ts" ) );
            }
            e.TypeFile.File.Imports.EnsureImport( fModel, "CommandModel" );
        }

    }
}
