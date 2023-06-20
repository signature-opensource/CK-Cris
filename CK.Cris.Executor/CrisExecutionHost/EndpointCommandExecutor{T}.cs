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
    public abstract class EndpointCommandExecutor<T> : EndpointCommandExecutor, ISingletonAutoService where T : class, EndpointDefinition.IScopedData
    {
        readonly CrisExecutionHost _executionHost;
        readonly IEndpointType<T> _endpoint;

        public EndpointCommandExecutor( CrisExecutionHost executionHost, IEndpointType<T> endpoint )
        {
            _executionHost = executionHost;
            _endpoint = endpoint;
        }

        /// <summary>
        /// Gets the execution host used by this executor.
        /// The same execution host can be used by multiple executors at the same time since 
        /// host's responsibility is only to manage the runners.
        /// </summary>
        public CrisExecutionHost ExecutionHost => _executionHost;

        /// <summary>
        /// Gets the endpoint that executes the commands.
        /// </summary>
        public IEndpointType<T> Endpoint => _endpoint;

        internal override AsyncServiceScope CreateAsyncScope( CrisJob job )
        {
            return _endpoint.GetContainer().CreateAsyncScope( Unsafe.As<T>( job._scopedData ) );
        }
    }

}
