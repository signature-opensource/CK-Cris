Executes Cris commands in a dedicated DI container, away from whatever received them.

Submitting returns immediately with a handle: await it for the result, or watch its live collection of
immediate events while the command runs. A scoped helper captures the caller's ambient services so
culture and authentication follow the command across.

A command submitted without them is not refused: its own restore methods rebuild an ambient service
hub before the scope is created, which is what lets a timer or a queue reader submit one. Failing to
rebuild yields an error result and a null service provider in the completion callback.
