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
        readonly string _serviceProviderName;
        readonly bool _hasMonitor;
        readonly Dictionary<Type, string> _cached;
        readonly IFunctionScope _function;
        ICodeWriter _variablesPart;
        int _lastPartVarCount;

        /// <summary>
        /// Initializes a ne< <see cref="VariableCachedServices"/>.
        /// </summary>
        /// <param name="engineMap">The engine map.</param>
        /// <param name="function">The function scope.</param>
        /// <param name="hasMonitor">True if a "monitor" variable is available.</param>
        /// <param name="serviceProviderName">Default IServiceProvider variable name is "s".</param>
        public VariableCachedServices( IStObjMap engineMap, IFunctionScope function, bool hasMonitor, string serviceProviderName = "s" )
        {
            _engineMap = engineMap;
            _function = function;
            _serviceProviderName = serviceProviderName;
            _hasMonitor = hasMonitor;
            _cached = new Dictionary<Type, string>();
            _variablesPart = function.CreatePart();
        }

        /// <summary>
        /// Gets whether a a "IActivityMonitor monitor" variable is available.
        /// </summary>
        public bool HasMonitor => _hasMonitor;

        /// <summary>
        /// Starts a new cached variable section is the function body.
        /// <para>
        /// This is used to avoid a resolution of all the services for nothing if an exit condition makes
        /// them useless.
        /// </para>
        /// </summary>
        public void StartNewCachedVariablesPart()
        {
            if( _lastPartVarCount > 0 )
            {
                _variablesPart = _function.CreatePart();
                _lastPartVarCount = 0;
            }
        }

        /// <summary>
        /// Gets the reusable local variable name to use for a registered DI service.
        /// </summary>
        /// <param name="serviceType">Type of the service to resolve.</param>
        /// <returns>The local variable name.</returns>
        public string GetServiceVariableName( Type serviceType )
        {
            if( _hasMonitor && serviceType.IsAssignableFrom( typeof( IActivityMonitor ) ) )
            {
                return "monitor";
            }
            serviceType = _engineMap.ToLeaf( serviceType )?.ClassType ?? serviceType;
            if( !_cached.TryGetValue( serviceType, out var name ) )
            {
                if( _cached.Count == 0 ) _variablesPart.GeneratedByComment( "Cached services variables" );
                name = $"cs{_cached.Count}";
                _variablesPart.Append( "var " ).Append( name )
                              .Append( " = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<" )
                              .AppendGlobalTypeName( serviceType ).Append( ">( " ).Append( _serviceProviderName ).Append( " );" ).NewLine();
                _cached[serviceType] = name;
                _lastPartVarCount++;
            }
            return name;
        }

        /// <summary>
        /// Writes either <see cref="GetServiceVariableName(Type)"/> of the <paramref name="serviceType"/>
        /// or <c>((FinalType)serviceVariableName)</c> if <paramref name="finalType"/> is not the same as <paramref name="serviceType"/>.
        /// <para>
        /// This nicely handles explicit implementation methods on <paramref name="finalType"/>.
        /// </para>
        /// </summary>
        /// <param name="w">The writer.</param>
        /// <param name="finalType">The final type to obtain.</param>
        /// <param name="serviceType">The registered DI service.</param>
        /// <returns>The writer.</returns>
        public ICodeWriter WriteExactType( ICodeWriter w, Type? finalType, Type serviceType )
        {
            if( finalType != null && finalType != serviceType )
            {
                w.Append( "((" ).AppendGlobalTypeName( finalType ).Append( ")" )
                    .Append( GetServiceVariableName( serviceType ) ).Append( ")" );
            }
            else
            {
                w.Append( GetServiceVariableName( serviceType ) );
            }
            return w;
        }
    }

}
