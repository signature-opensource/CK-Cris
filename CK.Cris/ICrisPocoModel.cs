using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Cris;

/// <summary>
/// Describes command properties and its unique and zero-based index in a context.
/// </summary>
public interface ICrisPocoModel
{
    /// <summary>
    /// Gets the kind of Cris Poco object.
    /// </summary>
    CrisPocoKind Kind { get; }

    /// <summary>
    /// Gets the command type: this is the final type that implements the <see cref="ICrisPoco"/> object.
    /// </summary>
    Type CommandType { get; }

    /// <summary>
    /// Creates a new <see cref="ICommand"/>, <see cref="ICommand{TResult}"/> or <see cref="IEvent"/> instance.
    /// </summary>
    ICrisPoco Create();

    /// <summary>
    /// Gets a unique index of this model in the <see cref="CrisDirectory.CrisPocoModels"/>.
    /// </summary>
    int CrisPocoIndex { get; }

    /// <summary>
    /// Gets the command or event name.
    /// </summary>
    string PocoName { get; }

    /// <summary>
    /// Gets the command or event previous names if any.
    /// </summary>
    IReadOnlyList<string> PreviousNames { get; }

    /// <summary>
    /// Gets the final (most specialized) result type.
    /// This is typeof(void) for <see cref="ICommand"/> or <see cref="IEvent"/>.
    /// </summary>
    Type ResultType { get; }

    /// <summary>
    /// Gets whether this command or event must configure the <see cref="AmbientServiceHub"/> before
    /// being executed in a endpoint context (at least one [ConfigureAmbientService] method exists for it).
    /// <para>
    /// This applies to incoming command: when true it means that the receiving endpoint context services
    /// may not be adapted to the execution of the command and that it may be executed in a correctly configured
    /// background context.
    /// </para>
    /// </summary>
    bool EndpointMustConfigureServices { get; }

    /// <summary>
    /// Gets whether this command or event when executed in the background without a <see cref="AmbientServiceHub"/>
    /// provided by a receiving endpoint must configure the background services before being executed (at least one
    /// [RestoreAmbientService] method exists for it).
    /// </summary>
    bool BackgroundMustRestoreServices { get; }

    /// <summary>
    /// Gets all the [AmbientServiceValue] property names that this command or event exposes.
    /// </summary>
    ImmutableArray<string> AmbientValuePropertyNames { get; }

    /// <summary>
    /// Gets whether this command or event is handled: <see cref="Handlers"/> is not empty.
    /// <para>
    /// A <see cref="CrisPocoKind.CallerOnlyEvent"/> (a <see cref="IEvent"/> without [RoutedEvent])
    /// is never handled.
    /// </para>
    /// </summary>
    bool IsHandled { get; }

    /// <summary>
    /// Exposes the description of a method that handles a command or routed event.
    /// <para>
    /// This should expose the documentation of the method (from Xml generated).
    /// </para>
    /// </summary>
    public interface IHandler
    {
        /// <summary>
        /// Gets the <see cref="IStObjFinalClass"/> that handles this command.
        /// <para>
        /// This is either a <see cref="IStObjFinalImplementation"/> (for real objects)
        /// or a <see cref="IStObjServiceClassDescriptor"/> (for Automatic Services).
        /// </para>
        /// </summary>
        IStObjFinalClass Type { get; }

        /// <summary>
        /// Gets the name of the method that handles this command.
        /// </summary>
        string MethodName { get; }

        /// <summary>
        /// Gets this handler kind.
        /// </summary>
        CrisHandlerKind Kind { get; }

        /// <summary>
        /// Gets the parameter types of <see cref="MethodName"/>.
        /// </summary>
        IReadOnlyList<Type> Parameters { get; }

        /// <summary>
        /// Gets the name of the file that implements the handler.
        /// </summary>
        string FileName { get; }

        /// <summary>
        /// Gets the line number in <see cref="FileName"/>.
        /// </summary>
        int LineNumber { get; }
    }

    /// <summary>
    /// Gets all the <see cref="IHandler"/> for this command or event.
    /// When empty, no handler has been found and the command or event cannot
    /// be executed in this process.
    /// </summary>
    ImmutableArray<IHandler> Handlers { get; }
}
