using System;
using System.IO;
using System.Net.Http.HPack;
using System.Text;

namespace hpack1
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] data = File.ReadAllBytes(args[0]);

            HPackDecoder decoder = new HPackDecoder();

            try
            {
                decoder.Decode(data, true, (state, name, value) =>
                {
                    Console.WriteLine(Encoding.ASCII.GetString(name) + ": " + Encoding.ASCII.GetString(value));
                }, null);

                decoder.CompleteDecode();
            }
            catch(HPackDecodingException)
            {
            }
        }
    }
}
