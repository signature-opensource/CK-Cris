using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace CK.Cris
{
    /// <summary>
    /// Base class for command executors bound to a specific <see cref="EndpointDefinition{TScopeData}"/>.
    /// Its responsibility is to expose a "Start" or "Submit" method that takes a <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
    /// (and any other parameter), setup a <see cref="CrisJob"/> and call <see cref="CrisExecutionHost.StartJob(CrisJob)"/>.
    /// <para>
    /// It can optionally override the <see cref="EndpointCommandExecutor"/> virtual methods to communicate execution outcomes
    /// to the external world.
    /// </para>
    /// <para>
    /// The "Start" or "Submit" method may setup and return a <see cref="IExecutingCommand{T}"/> if needed.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The scoped data type of the endpoint.</typeparam>
    [CKTypeDefiner]
    public abstract class EndpointCommandExecutor<T> : EndpointCommandExecutor, ISingletonAutoService where T : class, DIContainerDefinition.IScopedData
    {
        readonly CrisExecutionHost _executionHost;
        readonly IDIContainer<T> _container;

        public EndpointCommandExecutor( CrisExecutionHost executionHost, IDIContainer<T> container )
        {
            _executionHost = executionHost;
            _container = container;
        }

        /// <summary>
        /// Gets the execution host used by this executor.
        /// The same execution host can be used by multiple executors at the same time since 
        /// host's responsibility is only to manage the runners.
        /// </summary>
        public CrisExecutionHost ExecutionHost => _executionHost;

        /// <summary>
        /// Gets the container that executes the commands.
        /// </summary>
        public IDIContainer<T> DIContainer => _container;

        internal override AsyncServiceScope CreateAsyncScope( CrisJob job )
        {
            return _container.GetContainer().CreateAsyncScope( Unsafe.As<T>( job._scopedData ) );
        }
    }

}
