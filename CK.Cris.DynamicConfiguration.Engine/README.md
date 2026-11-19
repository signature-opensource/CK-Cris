# CK.Cris.DynamicConfiguration.Engine

Generates the `RunPropertyDirectory` of CK.Cris.DynamicConfiguration: the run properties of the types of the App that
declare a section (`[ConfigurationSection]`, their `[ConfigurationLevel( Run )]` properties: `ConfigurationRunProperty`
of CK.Engine.Configuration), with typed accessors from the root services (no reflection at run time). The ones that the
configuration of the App restricts below Run (`BinPathConfiguration.LockedRunValues`, written by the App builder from
the `"$LOCK"`) are generated as not changeable. A type that declares a section must be an App singleton.
