using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.HPack;
using System.Text;

namespace hpack1
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = args[0];

            var exceptions =
                from filePath in Directory.EnumerateFiles(path, "*.bin", SearchOption.TopDirectoryOnly)
                from exception in RunOne(filePath)
                group (exception, filePath) by exception.StackTrace into grp
                select grp
                    .OrderByDescending(ex_fp => new FileInfo(ex_fp.filePath).Length)
                    .First();

            foreach (var (exception, filePath) in exceptions)
            {
                Console.WriteLine(filePath);
                Console.WriteLine("===============");
                Console.WriteLine(exception.ToString());
                Console.WriteLine();
            }
        }

        static IEnumerable<Exception> RunOne(string filePath)
        {
            try
            {
                RunOneImpl(filePath);
                return Enumerable.Empty<Exception>();
            }
            catch (AggregateException ex)
            {
                return ex.Flatten().InnerExceptions;
            }
            catch (Exception ex)
            {
                return new[] { ex };
            }
        }

        static void RunOneImpl(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);

            HPackDecoder decoder = new HPackDecoder();

            try
            {
                decoder.Decode(data, true, (state, name, value) =>
                {
                    //Console.WriteLine(Encoding.ASCII.GetString(name) + ": " + Encoding.ASCII.GetString(value));
                }, null);

                decoder.CompleteDecode();
            }
            catch(HPackDecodingException)
            {
            }
        }
    }
}
