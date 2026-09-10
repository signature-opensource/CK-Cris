Sends Cris commands to a remote party over HTTP.

It is an application identity feature rather than a service: a remote whose configuration carries a
`CrisHttpSender` section gets one, and the endpoint is derived from the remote's address - which is
why an address with a path or a query is refused rather than silently rewritten.

Three send methods differ only in how they fail: one never throws and returns the error as the result,
one throws it, one unwraps the value. Retrying is on by default, the deadline covers the whole retry
sequence rather than each attempt, and the bearer token maintains itself from the login, refresh and
logout commands passing through.
