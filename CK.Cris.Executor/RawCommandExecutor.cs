using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Executes commands on services provided to <see cref="ExecuteCommandAsync(IActivityMonitor, IServiceProvider, ICommand)"/>.
    /// This class is agnostic of the context since the <see cref="IServiceProvider"/> defines the execution context: this is a true
    /// singleton, the same instance can be used to execute any locally handled commands.
    /// <para>
    /// The concrete class implements all the generated code that routes the command to its handler.
    /// </para>
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.RawCommandExecutorImpl, CK.Cris.Executor.Engine" )]
    public abstract class RawCommandExecutor : ISingletonAutoService
    {
        /// <summary>
        /// Executes a command by calling the ExecuteCommand or ExecuteCommandAsync method for the
        /// closure of the command Poco (the ICommand interface that unifies all other ICommand and <see cref="ICommandPart"/>).
        /// Any exceptions are thrown (or more precisely are set on the returned <see cref="Task"/>).
        /// </summary>
        /// <param name="services">The service context from which any required dependencies must be resolved.</param>
        /// <param name="command">The command to execute.</param>
        /// 
        /// <returns>The result of the <see cref="ICommand{TResult}"/> if the command has a result.</returns>
        public abstract Task<object?> RawExecuteCommandAsync( IServiceProvider services, ICommand command );
    }
}
