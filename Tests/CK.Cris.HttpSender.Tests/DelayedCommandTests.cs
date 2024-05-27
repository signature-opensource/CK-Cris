using CK.AppIdentity;
using CK.AspNet.Auth.Cris;
using CK.AspNet.Auth;
using CK.Auth;
using CK.Core;
using CK.Cris.AmbientValues;
using CK.Cris.AspNet;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;

namespace CK.Cris.HttpSender.Tests
{

    public interface IFullCommand : ICommand, ICommandAuthNormal, ICommandAuthImpersonation, ICommandAuthDeviceId, ICommandCurrentCulture
    {
        string Prefix { get; set; }
    }

    public sealed class FullCommandService : ISingletonAutoService
    {
        static DateTime _start;
        static long GetDeltaMS() => (DateTime.UtcNow - _start).Ticks / TimeSpan.TicksPerMillisecond;
        public static void Start()
        {
            _start = DateTime.UtcNow;
            Messages.Clear();
        }
        public static readonly ConcurrentBag<string> Messages = new ConcurrentBag<string>();

        [CommandHandler]
        public void Handle( IFullCommand c, IAuthenticationInfo auth, CurrentCultureInfo culture )
        {
            Messages.Add( $"{c.Prefix}-{auth.User.UserName}-{auth.ActualUser.UserName}-{auth.DeviceId.Length}-{culture.CurrentCulture.Name}-{GetDeltaMS()}" );
        }
    }

    [TestFixture]
    public class DelayedCommandTests
    {
        [Test]
        [CancelAfter(15000)]
        public async Task simple_delayed_command_Async( CancellationToken cancellation )
        {
            // We need the fr culture for this test.
            NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr" );

            await using var runningServer = await TestHelper.RunAspNetServerAsync(
                                                new[]
                                                {
                                                    typeof( IAuthenticationInfo ),
                                                    typeof( CrisAspNetService ),
                                                    typeof( AmbientValuesService ),
                                                    typeof( IDelayedCommand ),
                                                    typeof( CrisDelayedCommandService ),
                                                    typeof( IFullCommand ),
                                                    typeof( FullCommandService ),
                                                    typeof( IBasicLoginCommand ),
                                                    typeof( ILogoutCommand ),
                                                    typeof( IRefreshAuthenticationCommand ),
                                                    typeof( IAuthenticationResult ),
                                                    typeof( IPocoAuthenticationInfo ),
                                                    typeof( IPocoUserInfo ),
                                                    typeof( CrisAuthenticationService ),
                                                    typeof( CrisWebFrontAuthCommandHandler )
                                                },
                                                configureServices: services =>
                                                {
                                                    // We could have used the type registration above and
                                                    // benefit of the Automatic DI.
                                                    services.AddSingleton<FakeUserDatabase>();
                                                    services.AddSingleton<IUserInfoProvider>( sp => sp.GetRequiredService<FakeUserDatabase>() );
                                                    services.AddSingleton<FakeWebFrontLoginService>();
                                                    services.AddSingleton<IWebFrontAuthLoginService>( sp => sp.GetRequiredService<FakeWebFrontLoginService>() );
                                                } );

            var serverAddress = runningServer.ServerAddress;

            var callerServices = new[] { typeof( IDelayedCommand ),
                                         typeof( IFullCommand ),
                                         typeof( IBasicLoginCommand ),
                                         typeof( ILogoutCommand ),
                                         typeof( IRefreshAuthenticationCommand ),
                                         typeof( IAuthenticationResult ),
                                         typeof( IPocoAuthenticationInfo ),
                                         typeof( IPocoUserInfo ),
                                         typeof( CrisDirectory ),
                                         typeof( CommonPocoJsonSupport ),
                                         typeof( ApplicationIdentityService ),
                                         typeof( CrisHttpSenderFeatureDriver )};

            await using var runningCaller = await TestHelper.CreateRunningCallerAsync( serverAddress, callerServices, generateSourceCode: false );

            var callerPoco = runningCaller.Services.GetRequiredService<PocoDirectory>();
            var sender = runningCaller.ApplicationIdentityService.Remotes
                                                    .Single( r => r.PartyName == "$Server" )
                                                    .GetRequiredFeature<CrisHttpSender>();

            FullCommandService.Start();

            // Login Albert.
            var loginCommand = callerPoco.Create<IBasicLoginCommand>( c =>
            {
                c.UserName = "Albert";
                c.Password = "success";
            } );
            var loginAlbert = await sender.SendAndGetResultOrThrowAsync( TestHelper.Monitor, loginCommand, cancellationToken: cancellation );
            loginAlbert.Info.User.UserName.Should().Be( "Albert" );

            // IFullCommand requires Normal authentication. 
            // Using Albert (2) and the device identifier.
            var fullCommand = callerPoco.Create<IFullCommand>();
            fullCommand.Prefix = "n°1";
            fullCommand.ActorId = loginAlbert.Info.User.UserId;
            fullCommand.ActualActorId = loginAlbert.Info.ActualUser.UserId;
            fullCommand.DeviceId = loginAlbert.Info.DeviceId;

            // Baseline: Albert (null current culture name): this is executed in the Global DI context.
            await sender.SendOrThrowAsync( TestHelper.Monitor, fullCommand, cancellationToken: cancellation );
            FullCommandService.Messages.Single().Should().Match( "n°1-Albert-Albert-22-en-*" );

            // Delayed command now.
            var delayed = callerPoco.Create<IDelayedCommand>();
            delayed.Command = fullCommand;
            delayed.ExecutionDate = DateTime.UtcNow.AddMilliseconds( 150 );

            FullCommandService.Messages.Clear();
            await sender.SendOrThrowAsync( TestHelper.Monitor, delayed, cancellationToken: cancellation );
            while( FullCommandService.Messages.IsEmpty )
            {
                await Task.Delay( 50, cancellation );
            }
            FullCommandService.Messages.Single().Should().Match( "n°1-Albert-Albert-22-en-*" );

            delayed.ExecutionDate = DateTime.UtcNow.AddMilliseconds( 150 );
            fullCommand.DeviceId = "not-the-device-id";
            var executedCommand = await sender.SendAsync( TestHelper.Monitor, delayed, cancellationToken: cancellation );

            var error = executedCommand.Result as ICrisResultError;
            Throw.DebugAssert( error is not null );
            error = (ICrisResultError)executedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "Invalid device identifier: " );

            // Logout.
            await sender.SendOrThrowAsync( TestHelper.Monitor, callerPoco.Create<ILogoutCommand>(), cancellationToken: cancellation );

            // No more allowed.
            delayed.ExecutionDate = DateTime.UtcNow.AddMilliseconds( 150 );
            executedCommand = await sender.SendAsync( TestHelper.Monitor, delayed, cancellationToken: cancellation );
            error = executedCommand.Result as ICrisResultError;
            Throw.DebugAssert( error is not null );
            error = (ICrisResultError)executedCommand.Result!;
            error.IsValidationError.Should().BeTrue();
            error.Errors[0].Text.Should().StartWith( "Invalid actor identifier: " );

        }
    }
}
