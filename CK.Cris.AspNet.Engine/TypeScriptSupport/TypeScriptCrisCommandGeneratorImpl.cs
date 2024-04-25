using CK.CodeGen;
using CK.Core;
using CK.Cris;
using CK.Cris.EndpointValues;
using CK.Cris.AspNet;
using CK.Setup.Cris;
using CK.TypeScript.CodeGen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Setup
{
    public sealed partial class TypeScriptCrisCommandGeneratorImpl : ITSCodeGenerator
    {
        TSBasicType? _crisPoco;
        TSBasicType? _abstractCommand;
        TSBasicType? _command;
        TypeScriptFile? _modelFile;

        bool ITSCodeGenerator.Initialize( IActivityMonitor monitor, ITypeScriptContextInitializer initializer )
        {
            // This can be called IF multiple contexts must be generated:
            // we reset the cached instance here.
            _command = null;

            // Those must be TypeScript types: this ensures that they are added to the TypeScript type set.
            return initializer.EnsureRegister( monitor, typeof( IAspNetCrisResult ), mustBePocoType: true )
                   && initializer.EnsureRegister( monitor, typeof( IAspNetCrisResultError ), mustBePocoType: true )
                   && initializer.EnsureRegister( monitor, typeof( IEndpointValues ), mustBePocoType: true )
                   && initializer.EnsureRegister( monitor, typeof( IEndpointValuesCollectCommand ), mustBePocoType: true );
        }

        bool ITSCodeGenerator.StartCodeGeneration( IActivityMonitor monitor, TypeScriptContext context )
        {
            context.PocoCodeGenerator.PrimaryPocoGenerating += OnPrimaryPocoGenerating;
            context.PocoCodeGenerator.AbstractPocoGenerating += OnAbstractPocoGenerating;
            context.AfterCodeGeneration += OnAfterCodeGeneration;
            return true;
        }

        // We don't add anything to the default IPocoType handling.
        bool ITSCodeGenerator.OnResolveObjectKey( IActivityMonitor monitor, TypeScriptContext context, RequireTSFromObjectEventArgs e ) => true;

        void OnAbstractPocoGenerating( object? sender, GeneratingAbstractPocoEventArgs e )
        {
            // Filtering out redundant ICommand, ICommand<T>: in TypeScript type name is
            // unique (both are handled by ICommand<TResult = void>).
            // On the TypeScript side, we have always a ICommand<T> where T can be void.

            // By filtering out the base interface it doesn't appear in the base interfaces
            // nor in the branded type. 
            if( HasICommand( e.AbstractPocoType, e.ImplementedInterfaces, out var mustRemoveICommand ) && mustRemoveICommand )
            {
                e.ImplementedInterfaces = e.ImplementedInterfaces.Where( i => i.Type != typeof( ICommand ) );
            }
        }

        static bool HasICommand( IPocoType t, IEnumerable<IAbstractPocoType> implementedInterfaces, out bool mustRemoveICommand )
        {
            IPocoType? typedResult = null;
            bool hasICommand = false;
            foreach( var i in implementedInterfaces )
            {
                if( i.GenericTypeDefinition?.Type == typeof( ICommand<> ) )
                {
                    var tResult = i.GenericArguments[0].Type;
                    if( typedResult != null )
                    {
                        // This has been already checked.
                        throw new CKException( $"{t} returns both '{typedResult}' and '{tResult}'." );
                    }
                    typedResult = tResult;
                }
                if( i.Type == typeof( ICommand ) )
                {
                    hasICommand = true;
                }
            }
            mustRemoveICommand = hasICommand && typedResult != null;
            return hasICommand || typedResult != null;
        }

        void OnPrimaryPocoGenerating( object? sender, GeneratingPrimaryPocoEventArgs e )
        {
            if( e.PrimaryPocoType.Type == typeof(IEndpointValues) )
            {
                // Generate the EndpointValuesOverride when generating the EndpointValues:
                // we use the EndpointValues fields.
                GenerateEndpointValuesOverride( e.PocoTypePart.File.Folder, e.Fields );
            }
            else if( HasICommand( e.PrimaryPocoType, e.ImplementedInterfaces, out var mustRemoveICommand ) )
            {
                if( mustRemoveICommand )
                {
                    e.ImplementedInterfaces = e.ImplementedInterfaces.Where( i => i.Type != typeof( ICommand ) );
                }
                e.PocoTypePart.File.Imports.EnsureImport( EnsureCrisCommandModel( e.Monitor, e.TypeScriptContext ), "ICommandModel" );
                e.PocoTypePart.NewLine()
                    .Append( "get commandModel(): ICommandModel { return " ).Append( e.TSGeneratedType.TypeName ).Append( ".#m; }" ).NewLine()
                    .NewLine()
                    .Append( "static #m = " )
                    .OpenBlock()
                        .Append( "applyEndpointValues( command: any, a: any, o: any )" )
                        .OpenBlock()
                        .InsertPart( out var applyPart )
                        .CloseBlock()
                    .CloseBlock();
                bool atLeastOne = false;
                foreach( var f in e.Fields )
                {
                    Throw.DebugAssert( f.TSField.PocoField.Originator is IPocoPropertyInfo );
                    if( ((IPocoPropertyInfo)f.TSField.PocoField.Originator).DeclaredProperties
                                .Any( p => p.CustomAttributesData.Any( a => a.AttributeType == typeof( EndpointValueAttribute ) ) ) )
                    {
                        // Documents it.
                        f.DocumentationExtension = b => b.AppendLine( "(This is an Ambient Value.)", startNewLine: true );
                        // Adds the assignment: this property comes from its ambient value.
                        if( atLeastOne ) applyPart.NewLine();
                        // Generates:
                        // if( command.color === undefined ) command.color = o.color !== null ? o.color : a.color;
                        applyPart.Append( "if( command." ).Append( f.TSField.FieldName ).Append( " === undefined ) command." )
                            .Append( f.TSField.FieldName )
                            .Append( " = o." ).Append( f.TSField.FieldName ).Append( " !== null ? o." )
                            .Append( f.TSField.FieldName ).Append( " : a." ).Append( f.TSField.FieldName ).Append( ";" ).NewLine();
                        atLeastOne = true;
                    }
                }
                if( !atLeastOne ) applyPart.Append( "// This command has no AmbientValue property." ).NewLine();
            }
        }

        public bool OnResolveType( IActivityMonitor monitor,
                                   TypeScriptContext context,
                                   RequireTSFromTypeEventArgs builder )
        {
            var t = builder.Type;
            // Hooks:
            //   - ICommand and ICommand<TResult>: they are both implemented by ICommand<TResult = void> in Model.ts.
            //   - IAbstractCommand and ICrisPoco.
            // 
            // Model.ts also implements ICommandModel, ExecutedCommand<T>, and CrisError.
            //
            if( t.Namespace == "CK.Cris" )
            {
                if( t.Name == "ICommand" || (t.IsGenericTypeDefinition && t.Name == "ICommand`1") )
                {
                    EnsureCrisCommandModel( monitor, context );
                    builder.ResolvedType = _command;
                }
                else if( t.Name == "IAbstractCommand" )
                {
                    EnsureCrisCommandModel( monitor, context );
                    builder.ResolvedType = _abstractCommand;
                }
                else if( t.Name == "ICrisPoco" )
                {
                    EnsureCrisCommandModel( monitor, context );
                    builder.ResolvedType = _crisPoco;
                }
            }
            return true;
        }

        static void GenerateEndpointValuesOverride( TypeScriptFolder endpointValuesFolder, ImmutableArray<TSNamedCompositeField> fields )
        {
            var b = endpointValuesFolder
                                .FindOrCreateManualFile( "EndpointValuesOverride.ts" )
                                .CreateType( "EndpointValuesOverride", null, null )
                                .TypePart;
            b.Append( """
                    /**
                    * To manage endpoint values overrides, we use the null value to NOT override:
                    *  - We decided to map C# null to undefined because working with both null
                    *    and undefined is difficult.
                    *  - Here, the null is used, so that undefined can be used to override with an undefined that will
                    *    be a null value on the C# side.
                    * All the properties are initialized to null in the constructor.
                    **/

                    """ )
             .Append( "export class EndpointValuesOverride" )
             .OpenBlock()
             .InsertPart( out var propertiesPart )
             .Append( "constructor()" )
                 .OpenBlock()
                 .InsertPart( out var ctorPart )
                 .CloseBlock();

            foreach( var f in fields )
            {
                propertiesPart.Append( "public " )
                              .Append( f.TSField.FieldName ).Append( ": " )
                              .AppendTypeName( f.TSField.TSFieldType ).Append("|null;").NewLine();
                ctorPart.Append( "this." ).Append( f.TSField.FieldName ).Append( " = null;" ).NewLine();
            }

        }


    }
}
