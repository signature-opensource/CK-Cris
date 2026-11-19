using CK.Core;
using System.Collections.Generic;

namespace CK.Cris.DynamicConfiguration;

/// <summary>
/// The store of the changes made while the application runs: an optional service (without it, the changes are lost at
/// the next start). CK.AppIdentity.DynamicConfiguration keeps them in the store of the App's root party.
/// </summary>
public interface IDynamicConfigurationStore : ISingletonAutoService
{
    /// <summary>
    /// Loads the records, by the path of their property.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The records (case insensitive keys).</returns>
    IReadOnlyDictionary<string, DynamicConfigurationRecord> Load( IActivityMonitor monitor );

    /// <summary>
    /// Saves the records (all of them: the store is replaced).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="records">The records, by the path of their property.</param>
    void Save( IActivityMonitor monitor, IReadOnlyDictionary<string, DynamicConfigurationRecord> records );
}

/// <summary>
/// A change made while the application runs: the value, and the configured value it overrode (when the configured value
/// changes, the latest decision is the configuration's: the record is dropped).
/// </summary>
/// <param name="Value">The value (its text, invariant culture), null for a null value.</param>
/// <param name="ConfiguredValue">The configured value when the change was made, null when there was none.</param>
public sealed record DynamicConfigurationRecord( string? Value, string? ConfiguredValue );
