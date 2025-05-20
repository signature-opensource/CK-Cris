using System;

namespace CK.Cris;

/// <summary>
/// The decorated concrete <see cref="IEvent"/> will be raised immediately.
/// This can decorate only the concrete <see cref="IEvent"/> root, not a <see cref="IEventPart"/>
/// nor an extension.
/// <para>
/// Without this attribute, the event is raised when, and if, the root command successfully completed.
/// </para>
/// </summary>
[AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
public sealed class ImmediateEventAttribute : Attribute
{
}
