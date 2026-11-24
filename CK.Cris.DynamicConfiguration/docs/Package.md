The dynamic configuration of an application: the `[ConfigurationLevel( Run )]` properties of its singletons are
initialized from the configuration, changed while it runs by commands, and the changes are kept.

The properties are the ones of the App singletons that declare a `[ConfigurationSection]`. They are initialized from the
configuration when the application starts (a recorded change wins unless the configured value changed since then) and
changed while it runs by the commands of CK.IO.DynamicConfiguration (`DynamicConfigurationService`,
`DynamicConfigurationHandlers`). The changes are kept by an optional `IDynamicConfigurationStore`.
Its engine, CK.Cris.DynamicConfiguration.Engine, generates the accessors of the properties (`RunPropertyDirectory`).
