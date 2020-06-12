using CK.CodeGen.Abstractions;
using CK.Core;
using CK.Cris;
using CK.Text;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CK.Setup.Cris
{
    /// <summary>
    /// Actual implementation that takes care of all the abstract properties.
    /// This doesn't handle abstract methods at all.
    /// </summary>
    public partial class FrontCommandExecutorImpl : IAutoImplementorType
    {
        /// <summary>
        /// Initializes a new implementor. The constructor is required.
        /// </summary>
        public FrontCommandExecutorImpl()
        {
        }

        IAutoImplementorMethod? IAutoImplementorType.HandleMethod( IActivityMonitor monitor, MethodInfo m ) => null;

        IAutoImplementorProperty? IAutoImplementorType.HandleProperty( IActivityMonitor monitor, PropertyInfo p ) => null;

        public bool Implement( IActivityMonitor monitor, Type classType, ICodeGenerationContext c, ITypeScope scope )
        {
            if( classType != typeof( FrontCommandExecutor ) ) throw new InvalidOperationException( "Applies only to the FrontCommandReceiver class." );
            var commands = CommandRegistry.FindOrCreate( monitor, c );
            if( commands == null ) return false;

            return true;
        }


    }

}
