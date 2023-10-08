using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Cris.HttpSender.Tests
{
    public interface ICommandColored : ICommandPart
    {
        string Color { get; set; }
    }

    public interface IColoredAmbientValues : AmbientValues.IAmbientValues
    {
        /// <summary>
        /// The color of <see cref="ICommandColored"/> commands.
        /// </summary>
        string Color { get; set; }
    }

    public interface IBeautifulCommand : ICommandColored, ICommand<string>
    {
        string Beauty { get; set; }
    }

    public class ColorService : IAutoService
    {
        [CommandPostHandler]
        public void GetColoredAmbientValues( AmbientValues.IAmbientValuesCollectCommand cmd, IColoredAmbientValues values )
        {
            values.Color = "Red";
        }

        [CommandHandler]
        public string HandleBeatifulCommand( IBeautifulCommand cmd )
        {
            return $"{cmd.Color} - {cmd.Beauty}";
        }
    }

    public interface IBeautifulWithOptionsCommand : IBeautifulCommand
    {
        /// <summary>
        /// Gets or sets the number of milliseconds that the command handling must take.
        /// </summary>
        public int WaitTime { get; set; }
    }

    public class WithOptionsService : IAutoService
    {
        [CommandHandler]
        public async Task<string> HandleAsync( IBeautifulWithOptionsCommand cmd )
        {
            await Task.Delay( cmd.WaitTime );
            return $"{cmd.Color} - {cmd.Beauty} - {cmd.WaitTime}";

        }
    }

}
