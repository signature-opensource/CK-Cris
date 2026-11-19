using CK.Core;
using System;
using System.Globalization;

namespace CK.Cris.DynamicConfiguration;

/// <summary>
/// A property of an App singleton that its code can change while the application runs (a <c>[ConfigurationLevel( Run )]</c>
/// property of a type that declares a <c>[ConfigurationSection]</c>). Its accessors are generated (no reflection).
/// </summary>
public sealed class RunProperty
{
    readonly Func<IServiceProvider, object?> _getter;
    readonly Action<IServiceProvider, object?> _setter;

    /// <summary>
    /// Initializes a new run property. Called by the generated <see cref="RunPropertyDirectory"/>.
    /// </summary>
    /// <param name="path">The path of the property: its section, then its name (':' separated).</param>
    /// <param name="valueType">The type of the value.</param>
    /// <param name="isRun">
    /// False when the configuration restricts it to the Init or Compile level: it is initialized, not changed.
    /// </param>
    /// <param name="getter">The getter, from the root services.</param>
    /// <param name="setter">The setter, from the root services (the value has the <paramref name="valueType"/>).</param>
    public RunProperty( string path, Type valueType, bool isRun, Func<IServiceProvider, object?> getter, Action<IServiceProvider, object?> setter )
    {
        Path = path;
        ValueType = valueType;
        IsRun = isRun;
        _getter = getter;
        _setter = setter;
    }

    /// <summary>
    /// Gets the path of the property: its section, then its name (':' separated), as in the configuration.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the type of the value.
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets whether it can be changed while the application runs: false when the configuration restricts it to the Init
    /// or Compile level (it is initialized from the configuration only).
    /// </summary>
    public bool IsRun { get; }

    /// <summary>
    /// Gets the current value.
    /// </summary>
    /// <param name="services">The root services.</param>
    /// <returns>The current value.</returns>
    public object? GetValue( IServiceProvider services ) => _getter( services );

    /// <summary>
    /// Sets the value: it must be of the <see cref="ValueType"/> (see <see cref="TryConvert"/>). The setter of the
    /// property may throw on an invalid value.
    /// </summary>
    /// <param name="services">The root services.</param>
    /// <param name="value">The value.</param>
    public void SetValue( IServiceProvider services, object? value ) => _setter( services, value );

    /// <summary>
    /// Converts a value (a string of the configuration, a number or a string of a command) to a basic type.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="type">The target type.</param>
    /// <param name="result">The converted value.</param>
    /// <param name="error">The error when the value can't be converted.</param>
    /// <returns>True on success.</returns>
    public static bool TryConvert( object? value, Type type, out object? result, out string? error )
    {
        result = null;
        error = null;
        if( value == null )
        {
            if( !type.IsValueType ) return true;
            error = $"A {type.Name} can't be null.";
            return false;
        }
        if( type.IsInstanceOfType( value ) )
        {
            result = value;
            return true;
        }
        var text = ToText( value )!;
        try
        {
            if( type == typeof( string ) ) result = text;
            else if( type.IsEnum )
            {
                if( text.Length == 0 || char.IsDigit( text[0] ) || text[0] == '-' || !Enum.TryParse( type, text, ignoreCase: true, out var e ) || !Enum.IsDefined( type, e ) )
                {
                    error = $"'{text}' is not a value of {type.Name}: {string.Join( ", ", Enum.GetNames( type ) )}.";
                    return false;
                }
                result = e;
            }
            else if( type == typeof( TimeSpan ) ) result = TimeSpan.Parse( text, CultureInfo.InvariantCulture );
            else if( type == typeof( Guid ) ) result = Guid.Parse( text );
            else if( type == typeof( bool ) ) result = bool.Parse( text );
            else result = Convert.ChangeType( text, type, CultureInfo.InvariantCulture );
            return true;
        }
        catch( Exception ex ) when( ex is FormatException or OverflowException or InvalidCastException or ArgumentException )
        {
            error = $"'{text}' is not a valid {type.Name}.";
            return false;
        }
    }

    /// <summary>
    /// Gets the text of a basic value (invariant culture: the one of the configuration), null for null.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Its text.</returns>
    public static string? ToText( object? value ) => value switch
    {
        null => null,
        string s => s,
        bool b => b ? "true" : "false",
        TimeSpan t => t.ToString( "c", CultureInfo.InvariantCulture ),
        IFormattable f => f.ToString( null, CultureInfo.InvariantCulture ),
        _ => value.ToString()
    };

    /// <inheritdoc />
    public override string ToString() => $"{Path} ({ValueType.Name})";
}
