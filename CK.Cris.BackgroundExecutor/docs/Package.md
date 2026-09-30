Executes Cris commands in a dedicated, background, DI container, away from receiving endpoint.

Submitting returns immediately with a handle: await it for the result, or watch its live collection of
immediate events while the command runs.

The ambient services magic enables contextual information required by the command execution (validated by
the receiving endpoint) to "follow the command": culture, authentication and other ambient services are
available in the background container.

