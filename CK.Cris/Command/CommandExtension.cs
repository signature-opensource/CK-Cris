using CK.Core;
using System;

namespace CK.Cris;

/// <summary>
/// Provides helpers on <see cref="IAbstractCommand"/>.
/// </summary>
public static class CommandExtension
{
    /// <summary>
    /// Creates a result Poco instance for a <see cref="ICommand{TResult}"/> where <typeparamref name="TResult"/>
    /// is a <see cref="IPoco"/>.
    /// </summary>
    /// <typeparam name="TResult">The command result type.</typeparam>
    /// <param name="cmd">This command.</param>
    /// <returns>A new IPoco result.</returns>
    public static TResult CreateResult<TResult>( this ICommand<TResult> cmd ) where TResult : IPoco
    {
        return ((IPocoGeneratedClass)cmd).Factory.PocoDirectory.Create<TResult>();
    }

    /// <summary>
    /// Creates a result Poco instance for a <see cref="ICommand{TResult}"/> where <typeparamref name="TResult"/>
    /// is a <see cref="IPoco"/> with a configurator.
    /// </summary>
    /// <typeparam name="TResult">The command result type.</typeparam>
    /// <param name="cmd">This command.</param>
    /// <param name="configure">Result configurator.</param>
    /// <returns>A new IPoco result.</returns>
    public static TResult CreateResult<TResult>( this ICommand<TResult> cmd, Action<TResult> configure ) where TResult : IPoco
    {
        return ((IPocoGeneratedClass)cmd).Factory.PocoDirectory.Create( configure );
    }
}
