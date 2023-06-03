using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Helper service that generates local "cs0", "cs1", ... variables initialized with
    /// a call to a service provider GetService for a given type.
    /// </summary>
    public sealed class VariableCachedServices
    {
        // TODO: MUST BE A ICodePart!
        readonly ICodeWriter _variablesPart;
        readonly string _serviceProviderName;
        Dictionary<Type, string> _cached;

        public VariableCachedServices( ICodeWriter variablesPart, string serviceProviderName = "s" )
        {
            _variablesPart = variablesPart;
            _serviceProviderName = serviceProviderName;
            _cached = new Dictionary<Type, string>();
        }

        public string? GetVariableName( Type type ) => _cached.GetValueOrDefault( type );

        public T WriteGetService<T>( T w, Type serviceType ) where T : ICodeWriter
        {
            if( !_cached.TryGetValue( serviceType, out var name ) )
            {
                if( _cached.Count == 0 ) _variablesPart.GeneratedByComment( "Cached services variables" );
                name = $"cs{_cached.Count}";
                var typeName = serviceType.ToGlobalTypeName();
                _variablesPart.Append( "var " ).Append( name )
                              .Append( " = (" )
                              .Append( typeName )
                              .Append( ")" ).Append( _serviceProviderName ).Append( ".GetService( typeof(" ).Append( typeName ).Append( ") );" ).NewLine();
                _cached[ serviceType ] = name;
            }
            return w.Append( name );
        }
    }

}
