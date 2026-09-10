The runtime side of Cris: validating an incoming command, then executing it.

Two process-wide singletons split the pipeline. The receiver runs the incoming validators and, when
the command needs it, produces an ambient service hub - the handover that lets the command execute in
a different DI container. The executor runs the handling validators, the handler and the post
handlers, taking the service provider as a parameter so one instance serves every context.

Failures come back as an error result with user messages and a log key rather than as exceptions,
event dispatch being the documented exception. A background host with a settable runner count and a
hub of routed events complete it.
