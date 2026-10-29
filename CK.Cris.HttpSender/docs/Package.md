Sends Cris commands to a remote party over HTTP. `CrisHttpSender` (`ICrisHttpSender`) is an application identity feature:
`CrisHttpSenderFeatureDriver` adds it to every remote whose configuration has a `CrisHttpSender` section, and it posts to the remote's
`/.cris/net` endpoint (the session commands to `/.cris/auth`). `SendAsync`, `SendOrThrowAsync` and `SendAndGetResultOrThrowAsync` differ only in how they fail; retrying, a deadline
that covers the whole retry sequence and the bearer token maintained from the login, refresh and logout commands are built in.
