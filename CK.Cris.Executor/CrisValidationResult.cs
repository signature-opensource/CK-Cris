using CK.Core;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Cris
{
    /// <summary>
    /// Captures the result of a <see cref="IEvent"/>, <see cref="ICommand"/> or <see cref="ICommand{TResult}"/>
    /// validation with potential warnings.
    /// <para>
    /// This is a simple immutable capture of a <see cref="UserMessageCollector"/>.
    /// </para>
    /// <para>
    /// This is not simply named "ValidationResult" because of the existing <see cref="System.ComponentModel.DataAnnotations.ValidationResult"/>.
    /// </para>
    /// </summary>
    public sealed class CrisValidationResult
    {
        /// <summary>
        /// The success validation result: no error, no warning, no information.
        /// </summary>
        public static readonly CrisValidationResult SuccessResult = new CrisValidationResult();

        /// <summary>
        /// A successfully completed validation task: no error, no warning, no information.
        /// </summary>
        public static readonly Task<CrisValidationResult> SuccessResultTask = Task.FromResult( SuccessResult );

        readonly ImmutableArray<UserMessage> _errorMessages;
        readonly ImmutableArray<UserMessage> _validationMessages;

        CrisValidationResult()
        {
            _errorMessages = ImmutableArray<UserMessage>.Empty;
            _validationMessages = ImmutableArray<UserMessage>.Empty;
        }

        /// <summary>
        /// Initializes a new validation result.
        /// </summary>
        /// <param name="messages">The messages.</param>
        /// <param name="logKey">Optional <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the validation.</param>
        public CrisValidationResult( IEnumerable<UserMessage> messages, string? logKey )
        {
            int count = 0;
            var bE = ImmutableArray.CreateBuilder<UserMessage>();
            foreach( var m in messages )
            {
                count++;
                if( m.Level == UserMessageLevel.Error )
                {
                    bE.Add( m );
                }
            }
            _errorMessages = bE.ToImmutable();
            bE = ImmutableArray.CreateBuilder<UserMessage>( count );
            bE.AddRange( messages );
            _validationMessages = bE.MoveToImmutable();
            LogKey = logKey;
        }

        /// <summary>
        /// Gets the error messages.
        /// </summary>
        public ImmutableArray<UserMessage> ErrorMessages => _errorMessages;

        /// <summary>
        /// Gets all the messages (including errors).
        /// </summary>
        public ImmutableArray<UserMessage> ValidationMessages => _validationMessages;

        /// <summary>
        /// Gets whether the command has been successfully validated: <see cref="ErrorMessages"/> is empty.
        /// </summary>
        public bool Success => _errorMessages.IsEmpty;

        /// <summary>
        /// <see cref="ActivityMonitor.LogKey"/> that enables to locate the logs of the validation.
        /// It may not always be available.
        /// </summary>
        public string? LogKey { get; }

    }
}
