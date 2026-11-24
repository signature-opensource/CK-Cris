The engine of CK.Cris.DynamicConfiguration: generates the accessors of the `[ConfigurationLevel( Run )]` properties of
the App singletons (no reflection at run time).

It generates the `RunPropertyDirectory` of an App, the typed accessors of these properties; the ones that the
configuration restricts below Run (a `"$LOCK"`) are initialized, not changed. Referenced by the builders.
