The dynamic configuration of an application: the `[ConfigurationLevel( Run )]` properties of the App singletons that
declare a `[ConfigurationSection]` are initialized from the configuration when the application starts (a recorded change
wins unless the configured value changed since then) and changed while it runs by the commands of CK.IO.DynamicConfiguration
(`DynamicConfigurationService`, `DynamicConfigurationHandlers`). The changes are kept by an optional `IDynamicConfigurationStore`.
Its engine, CK.Cris.DynamicConfiguration.Engine, generates the accessors of the properties (`RunPropertyDirectory`).
