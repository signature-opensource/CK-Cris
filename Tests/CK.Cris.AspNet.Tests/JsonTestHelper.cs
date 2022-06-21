using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CK.Cris.Tests
{
    public static class JsonTestHelper
    {
        /// <summary>
        /// Serializes the Poco in UTF-8 Json.
        /// </summary>
        /// <param name="o">The poco.</param>
        /// <param name="withType">True to emit types.</param>
        /// <returns>The bytes.</returns>
        public static ReadOnlyMemory<byte> Serialize( IPoco o, bool withType )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                o.Write( w, withType );
                w.Flush();
            }
            return m.WrittenMemory;
        }

        public static T? Deserialize<T>( IServiceProvider services, ReadOnlySpan<byte> b ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r );
        }

        public static T? Deserialize<T>( IServiceProvider services, string s ) where T : class, IPoco
        {
            return Deserialize<T>( services, Encoding.UTF8.GetBytes( s ) );
        }

        public static T? Roundtrip<T>( IServiceProvider services, T? o, IActivityMonitor? monitor = null ) where T : class, IPoco
        {
            byte[] bin1;
            string bin1Text;
            var directory = services.GetRequiredService<PocoDirectory>();
            using( var m = new MemoryStream() )
            {
                try
                {
                    using( var w = new Utf8JsonWriter( m ) )
                    {
                        o.Write( w, true );
                        w.Flush();
                    }
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                }
                catch( Exception )
                {
                    // On error, bin1 and bin1Text can be inspected here.
                    throw;
                }

                var o2 = directory.JsonDeserialize( bin1 );

                m.Position = 0;
                using( var w2 = new Utf8JsonWriter( m ) )
                {
                    o2.Write( w2, true );
                    w2.Flush();
                }
                var bin2 = m.ToArray();

                bin1.Should().BeEquivalentTo( bin2 );

                // On success, log.
                monitor?.Debug( bin1Text );

                // Is this an actual Poco or a definer?
                // When it's a definer, there is no factory!
                var f = services.GetService<IPocoFactory<T>>();
                if( f != null )
                {
                    var r2 = new Utf8JsonReader( bin2 );
                    var o3 = f.Read( ref r2 );
                    return o3;
                }
                return (T?)o2;
            }

        }
    }
}
