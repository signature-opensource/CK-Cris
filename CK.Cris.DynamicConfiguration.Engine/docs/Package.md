The engine of CK.Cris.DynamicConfiguration: generates the `RunPropertyDirectory` of an App, the typed accessors of the
`[ConfigurationLevel( Run )]` properties of its singletons (no reflection at run time); the ones that the configuration
restricts below Run (a `"$LOCK"`) are initialized, not changed. Referenced by the builders.
