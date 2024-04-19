using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Helper service that generates local "cs0", "cs1", ... variables initialized with
    /// a call to a service provider GetService for a given type that is as optimized as
    /// possible: with the help of the <see cref="IStObjMap.ToLeaf(Type)"/>, types that
    /// resolves to the same are grouped. 
    /// </summary>
    public sealed class VariableCachedServices
    {
        readonly IStObjMap _engineMap;
        readonly ICodeWriter _variablesPart;
        readonly string _serviceProviderName;
        readonly Dictionary<Type, string> _cached;

        public VariableCachedServices( IStObjMap engineMap, ICodeWriter variablesPart, string serviceProviderName = "s" )
        {
            _engineMap = engineMap;
            _variablesPart = variablesPart;
            _serviceProviderName = serviceProviderName;
            _cached = new Dictionary<Type, string>();
        }

        public string GetServiceVariableName( Type serviceType )
        {
            serviceType = _engineMap.ToLeaf( serviceType )?.ClassType ?? serviceType;
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
            return name;
        }
    }

}
