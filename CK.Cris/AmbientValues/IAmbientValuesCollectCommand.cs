namespace CK.Cris.AmbientValues;

/// <summary>
/// The command that returns the <see cref="IAmbientValues"/>.
/// <para>
/// This standard command comes with a default but rather definitive command handler (<see cref="AmbientValuesService.GetValues(IAmbientValuesCollectCommand)"/>)
/// that instantiates an empty IAmbientValues instance: then, any number of <see cref="CommandPostHandlerAttribute"/> can be used to set
/// the values.
/// </para>
/// </summary>
public interface IAmbientValuesCollectCommand : ICommand<IAmbientValues>
{
}
