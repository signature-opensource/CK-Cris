Setup-time code generator for the Cris executor and receiver: it generates the bodies of `RawCrisExecutor` and
`RawCrisReceiver` on the command classes themselves.

It is loaded by the setup engine at setup time (CK.Cris.Executor names it with `[assembly: RequiredEngine]`) and never
referenced by an application. `RawCrisExecutorImpl` and `RawCrisReceiverImpl` read the `ICrisDirectoryServiceEngine`
model of CK.Cris.Engine: executing a command is a virtual call, with no lookup at runtime.
