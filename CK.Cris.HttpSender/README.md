# Sending commands over HTTP

An application identity feature that turns a configured remote party into something you can send Cris
commands to. Configuration adds it; a resolved feature is the API.

## It is a feature on a remote, not a service you register.

[`CrisHttpSenderFeatureDriver`](CrisHttpSenderFeatureDriver.cs) walks every remote party at startup
and adds a [`CrisHttpSender`](CrisHttpSender.cs) to those whose configuration has a `"CrisHttpSender"`
section. The section may be the bare value `true` - the driver uses `ShouldApplyConfiguration`, so an
empty configuration is a valid one.

```jsonc
"Remotes": [
  {
    "PartyName": "$Backend",
    "Address": "https://backend.acme.com",
    "CrisHttpSender": {
      "Timeout": "00:00:30",
      "Retry": { "MaxRetryAttempts": 3, "BackoffType": "Exponential", "UseJitter": true }
    }
  }
]
```

The address is checked before anything is plugged, and the two refusals are errors that abort the
setup of that feature:

- it must parse as an absolute `http://` or `https://` URL;
- it must have **no path and no query** - `uri.PathAndQuery != "/"` is rejected.

The reason for the second is that the endpoint is derived, not configured: the sender always posts to
`<Address>/.cris/net`. A remote whose address already carries a path would silently produce a
different endpoint, so it is refused instead.

Retry is opt-**out** (`optOut: true`), timeout is not: omit `"Retry"` and you get the default retry
strategy, omit `"Timeout"` and you get 100 seconds. Setting `"Retry": false` is how you turn retrying
off.

Getting the sender is then a feature lookup on the remote party, and sending is one call:

```csharp
var sender = remote.GetRequiredFeature<CrisHttpSender>();
var executed = await sender.SendAsync( monitor, command );
if( executed.Result is ICrisResultError error ) { /* error.Errors, error.LogKey */ }
```

`GetFeature<T>()` returns null when the remote has no `"CrisHttpSender"` section;
`GetRequiredFeature<T>()` throws instead, which is what you want when the configuration is supposed to
guarantee it. Both come from `IParty`.

This repository has no test or sample for this package, so unlike the snippets elsewhere in this batch
that one is written from the API rather than lifted from a fixture.

## Three send methods, and they differ only in how they fail.

```csharp
Task<IExecutedCommand<T>> SendAsync<T>( IActivityMonitor monitor, T command, TimeSpan? timeout = null, ... );
Task<IExecutedCommand<T>> SendOrThrowAsync<T>( IActivityMonitor monitor, T command, ... );
Task<TResult>             SendAndGetResultOrThrowAsync<TResult>( IActivityMonitor monitor, ICommand<TResult> command, ... );
```

`SendAsync` **never throws** - a transport failure, a malformed response and a remote validation error
all arrive as an `ICrisResultError` in `IExecutedCommand.Result`, and the caller tests for it.
`SendOrThrowAsync` does the same call and converts that error into an exception via
`ICrisResultError.CreateException`. `SendAndGetResultOrThrowAsync` goes one step further and unwraps
the result value.

The `[CallerLineNumber]` and `[CallerFilePath]` parameters on all three reach the log line the sender
writes before sending. On the two throwing overloads they go further: they are passed to
`ICrisResultError.CreateException`, so the exception points at the call site rather than at the
sender. On `SendAsync`, which never throws, they are only log decoration.

The one error message the sender produces itself is `"Internal error."`, deliberately
non-translatable and deliberately vague: what actually happened is in the log, not in the user-facing
message. A `"Protocol error."` message is declared beside it and never used - the only reference to
`ProtocolErrorMessage` in the whole repository is its own declaration.

## The timeout is not the `HttpClient` timeout.

```csharp
_configuredTimeout = timeout ?? TimeSpan.FromSeconds( 100 );
_httpClient.Timeout = Timeout.InfiniteTimeSpan;
```

The client's own timeout is switched off and the deadline is carried per request, in an
`HttpRequestOptionsKey<TimeSpan>` read by
[`TokenAndTimeoutHandler`](CrisHttpSender.TokenAndTimeoutHandler.cs). That is what makes the
`timeout` parameter on each send method work at all - `HttpClient.Timeout` is per client, and it
would also cut the retry sequence short rather than each attempt.

The handler stack is built bottom-up in the constructor: `HttpClientHandler`, then optionally a Polly
`ResilienceHandler` carrying the retry strategy, then `TokenAndTimeoutHandler` on top. Being
outermost has two consequences. The deadline is a linked `CancellationTokenSource` covering the
**whole** retry sequence, not each attempt - three retries do not get three timeouts. And the bearer
header is stamped once, before the first attempt, so changing `AuthorizationToken` mid-flight does not
affect a request already retrying.

That handler also distinguishes the two ways a request stops: an `OperationCanceledException` raised
while the caller's own token is *not* cancelled is the deadline, and it is rethrown as a
`TimeoutException`.

`DisableServerCertificateValidation` accepts every certificate. It is one boolean, read from inside
the `"CrisHttpSender"` section rather than from the remote's root, and it is what it says.

## The authorization token maintains itself.

```csharp
public bool SkipAutomaticAuthorizationToken { get; set; }   // defaults to false
public string? AuthorizationToken { get; set; }
```

By default the sender watches for `IBasicLoginCommand`, `IRefreshAuthenticationCommand` and
`ILogoutCommand` going through it and updates the bearer token from their results. Log in once and
subsequent commands are authenticated with no further work.

Setting the token by hand is supported, and the property doc says what to do first: set
`SkipAutomaticAuthorizationToken` to true, or the next login result overwrites it.

The sender is described as *anonymous*, and the sentence that matters is the one about what it may
send: *"can send any command that are in the 'AllExchangeable' type set of the receiver"* - the
remote's Poco type set is the boundary, not anything declared here.

Every retry is logged as a warning under the `Cris` tag, naming the remote and the attempt number - so
a slow call that eventually succeeds leaves a trace rather than disappearing. The status code is in
there only when the attempt produced a response; an attempt that threw logs the exception instead.

## Requires.

- `CK.AppIdentity`, for `IRemoteParty`, `ApplicationIdentityFeatureDriver` and the feature lifetime.
- `CK.IO.Auth.Basic`, for the three authentication commands the sender recognizes.
- `CK.Poco.Exc.Json`, for reading and writing the commands and results on the wire.
- `Polly.Core`, for the retry pipeline.
