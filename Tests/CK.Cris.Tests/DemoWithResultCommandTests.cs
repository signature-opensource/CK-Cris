using CK.Auth;
using CK.Core;
using CK.Cris;
using NUnit.Framework;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Tests
{
    public class DemoWithResultCommandTests
    {
        public interface IAuthenticatedCommandPart : ICommandPart
        {
            int ActorId { get; set; }
        }

        [CK.Core.CKTypeDefiner]
        public abstract class CommandPartValidator<T> : CK.Core.ISingletonAutoService where T : ICommandPart
        {
        }

        [CK.Core.CKTypeDefiner]
        public interface IValidatedCommandPart<T> : CK.Core.IClosedPoco where T : ICommandPart
        {
            T CommandPart { get; set; }
        }

        [CK.Core.CKTypeDefiner]
        public interface IValidatedCommand<T> : CK.Core.IClosedPoco where T : ICommand
        {
            T Command { get; set; }
        }


        public interface IValidatedAuthenticatedCommandPart : IValidatedCommandPart<IAuthenticatedCommandPart>
        {
            IAuthenticationInfo AuthInfo { get; set; }
        }


        public class ValidatedCommandBuilder
        {
            public IActivityMonitor Monitor { get; }

            public ICommand Command { get; }

            /// <summary>
            /// Gets whether the validation succeeds. Defaults to true and becomes false
            /// as soon as an error or a fatal is logged into the <see cref="Monitor"/>.
            /// </summary>
            public bool Success { get; }

            /// <summary>
            /// Gets a 
            /// </summary>
            public SimpleServiceContainer AmbientServices { get; }
        }

        public class AuthenticatedCommandPartValidator : CommandPartValidator<IAuthenticatedCommandPart>
        {
            public void Validate( IActivityMonitor monitor, IAuthenticationInfo auth, IAuthenticatedCommandPart command, IValidatedAuthenticatedCommandPart validated )
            {
            }
        }

        /// <summary>
        /// This command's execution must provide an integer result.
        /// </summary>
        public interface IDemoCommandWithResult : ICommand<int>, IAuthenticatedCommandPart
        {
        }





        //[Test]
        //public void Command_model_test()
        //{
        //    Assert.Pass();
        //}
    }
}
