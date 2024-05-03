using CK.Core;
using System.Linq;

namespace CK.Setup.Cris
{

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

        internal bool RegisterAmbientValueDefinitionField( IActivityMonitor monitor, IBaseCompositeType owner, IBasePocoField field )
        {
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
                int more = _ambientValues.Count - _ambientValuesType.Fields.Count;
                if( more > 0 )
                {
                    var missing = _ambientValues.Where( a => !_ambientValuesType.Fields.Any( f => f.Name == a.Key ) );
                    Throw.DebugAssert( missing.Count() == more );

                    monitor.Error( $"""
                                    Missing IAmbientValues properties for [AmbientServiceValue] properties.
                                    Are you missing a 'IXXXAmbientValues : IAmbientValues' secondary Poco definition with the following properties?
                                    {missing.Select( m => $"'{m.Value.PropertyType.NonNullable.CSharpName} {m.Key} {{ get; set; }}'" ).Concatenate("', '" )}
                                    """ );
                    success = false;
                }
            }
            return success;
        }


    }

}
