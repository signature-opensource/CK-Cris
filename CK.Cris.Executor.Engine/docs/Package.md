Setup-time code generator for the Cris executor and receiver. Not referenced by applications: it is
loaded by the CKSetup engine.

It reads the model discovered by the Cris engine and emits the bodies of the two abstract runtime
classes. The generated code lives on the command classes themselves rather than in a dispatcher, so
executing a command is a virtual call and there is no lookup at runtime.

Missing pieces degrade instead of failing: a command with no handler gets a default implementation
returning an error naming it. Whether a generated method needs an async state machine is decided per
command, from whether any of its handlers is asynchronous.
