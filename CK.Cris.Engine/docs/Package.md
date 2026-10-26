Setup-time engine for Cris (`[assembly: IsEngine]`): it is loaded by the setup engine at setup time, named by string by the Cris attributes,
and never referenced by an application. `CrisDirectoryImpl` discovers every command, event and handler method once the Poco type system is
available, checks them (`CrisType` is the model of a command or an event), generates the `CrisDirectory` and publishes the model to the
other code generators as `ICrisDirectoryServiceEngine`.
