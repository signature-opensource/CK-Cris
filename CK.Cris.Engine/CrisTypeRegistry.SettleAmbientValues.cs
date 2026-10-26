using CK.Core;
using CK.Cris;
using System;
using System.Linq;

namespace CK.Setup.Cris;


internal sealed partial class CrisTypeRegistry
{
    sealed class AmbientValueEntry
    {
        public readonly IBaseCompositeType FirstOwner;
        public readonly IPocoType PropertyType;

        public AmbientValueEntry( IBaseCompositeType firstOwner, IPocoType propertyType )
        {
            FirstOwner = firstOwner;
            PropertyType = propertyType;
        }
    }

    internal bool RegisterAmbientValueDefinitionField( IActivityMonitor monitor, IBaseCompositeType owner, IBasePocoField field, bool isSafe )
    {
        // A value is safe as soon as one of its definitions says so: it is checked.
        if( isSafe ) _safeAmbientValueNames.Add( field.Name );
        // Updates the index by name and checks the property type accross definitions.
        if( !_ambientValues.TryGetValue( field.Name, out var already ) )
        {
            _ambientValues.Add( field.Name, already = new AmbientValueEntry( owner, field.Type ) );
        }
        else
        {
            if( already.PropertyType != field.Type )
            {
                monitor.Error( $"[AmbientServiceValue] property type '{field.Name}' differ: it is '{already.PropertyType.CSharpName}' for '{already.FirstOwner}' " +
                               $"and  '{field.Type.CSharpName}' for '{owner.CSharpName}'." );
                return false;
            }
        }
        // Registers the final field (the IPrimaryPocoField).
        if( field is IAbstractPocoField a )
        {
            foreach( var p in a.Implementations )
            {
                if( _allAmbientValueFields.Add( p ) )
                {
                    _indexedTypes[p.Owner].AddAmbientValueField( p );
                }
            }
        }
        else
        {
            Throw.DebugAssert( field is IPrimaryPocoField );
            var p = (IPrimaryPocoField)field;
            if( _allAmbientValueFields.Add( p ) )
            {
                _indexedTypes[p.Owner].AddAmbientValueField( p );
            }
        }
        return true;
    }

    /// <summary>
    /// Warns when a safe ambient value (<see cref="AmbientServiceValueAttribute.IsSafe"/>) of a handled type
    /// is checked by no [IncomingValidator]: since it is not checked, any caller can state it.
    /// <para>
    /// This should be an error: it is a warning until the packages that handle such commands in Systems
    /// without validators are migrated.
    /// </para>
    /// </summary>
    internal void CheckSafeAmbientValues( IActivityMonitor monitor, CrisType e )
    {
        if( !e.IsHandled ) return;
        foreach( var f in e.AmbientValueFields )
        {
            if( !_safeAmbientValueNames.Contains( f.Name ) ) continue;
            if( !e.IncomingValidators.Any( v => HasProperty( v.ThisPocoParameter.ParameterType, f.Name ) ) )
            {
                monitor.Warn( $"Safe ambient value '{f.Name}' of '{e.PocoName}' is checked by no [IncomingValidator]: any caller can state it. " +
                              $"An [IncomingValidator] for this property must be available (an authentication validator for the authentication parts)." );
            }
        }

        static bool HasProperty( Type t, string name )
        {
            return t.GetProperty( name ) != null || t.GetInterfaces().Any( i => i.GetProperty( name ) != null );
        }
    }

    internal bool SettleAmbientValues( IActivityMonitor monitor )
    {
        bool success = true;
        // We silently ignore the edge case where the IAmbientValues collector have been excluded.
        if( _ambientValuesType != null )
        {
            foreach( var f in _ambientValuesType.Fields )
            {
                if( _ambientValues.TryGetValue( f.Name, out var exist ) )
                {
                    if( exist.PropertyType != f.Type.Nullable )
                    {
                        monitor.Error( $"'IAmbientValues.{f.Name}' is of type '{f.Type}' but '{exist.FirstOwner.CSharpName}.{f.Name}' is '{exist.PropertyType.CSharpName}'." );
                        success = false;
                    }
                }
                else
                {
                    monitor.Info( $"'IAmbientValues.{f.Name}' doesn't correspond to any discovered Cris command or event [AmbientServiceValue] properties." );
                }
            }
            // IAmbientValues properties that correspond to no [AmbientServiceValue] are allowed (see above):
            // the missing ones must be computed, not inferred from the counts.
            var missing = _ambientValues.Where( a => !_ambientValuesType.Fields.Any( f => f.Name == a.Key ) ).ToList();
            if( missing.Count > 0 )
            {
                monitor.Error( $"""
                                Missing IAmbientValues properties for [AmbientServiceValue] properties.
                                Are you missing a 'IXXXAmbientValues : IAmbientValues' secondary Poco definition with the following properties?
                                {missing.Select( m => $"'{m.Value.PropertyType.NonNullable.CSharpName} {m.Key} {{ get; set; }}'" ).Concatenate( "', '" )}
                                """ );
                success = false;
            }
        }
        return success;
    }


}
