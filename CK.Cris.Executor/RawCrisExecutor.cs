using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Executes commands on services provided to <see cref="ExecuteCommandAsync(IActivityMonitor, IServiceProvider, ICrisPoco)"/>.
    /// This class is agnostic of the context since the <see cref="IServiceProvider"/> defines the execution context: this is a true
    /// singleton, the same instance can be used to execute any locally handled commands.
    /// <para>
    /// The concrete class implements all the generated code that routes the command to its handler.
    /// </para>
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCrisExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCrisExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Executes a command or an event by calling the discovered handlers and post handlers.
        /// Any exceptions are thrown (or more precisely are set on the returned <see cref="Task"/>).
        /// <para>
        /// A <see cref="IActivityMonitor"/> and a <see cref="ICrisExecutionContext"/> (that is
        /// a <see cref="ICrisCallContext"/>) must be resolvable from the <paramref name="services"/>.
        /// </para>
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="o">The command or event to execute.</param>
        /// <returns>The result of the <see cref="ICommand{TResult}"/> if the command has a result.</returns>
        public abstract Task<object?> RawExecuteAsync( IServiceProvider services, ICrisPoco o );
    }
}
