using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

using Huffman = System.Net.Http.HPack.Huffman;

namespace System.Net.Http.Unit.Tests.HPack
{
    public class HuffmanEncodingTests
    {
        [Theory]
        [MemberData(nameof(GetConcatenateHeaderValuesData))]
        public void HuffmanEncode_ConcatenateHeaderValues_RoundTrip(string[] values, string separator, bool lowerCase)
        {
            int maximumLength = values.Sum(x => x.Length + separator.Length) * 2;

            byte[] encodedBuffer = new byte[1];
            if (!Huffman.TryEncode(values, separator, lowerCase, encodedBuffer, out int encodedLength))
            {
                Array.Resize(ref encodedBuffer, encodedLength);

                bool huffmanEncodeSuccess = Huffman.TryEncode(values, separator, lowerCase, encodedBuffer, out int finalEncodedLength);
                Assert.True(huffmanEncodeSuccess);
                Assert.Equal(encodedLength, finalEncodedLength);
            }

            byte[] decodedBuffer = new byte[1];
            int decodedLength = Huffman.Decode(encodedBuffer.AsSpan(0, encodedLength), ref decodedBuffer);
            string decodedString = Encoding.ASCII.GetString(decodedBuffer.AsSpan(0, decodedLength));

            string expectedDecodedString = string.Join(separator, values);
            if (lowerCase) expectedDecodedString = expectedDecodedString.ToLowerInvariant();

            Assert.Equal(expectedDecodedString, decodedString);
        }

        public static IEnumerable<object[]> GetConcatenateHeaderValuesData()
        {
            var singleValues =
                from v in new[]
                {
                    "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ=="
                }
                select new string[] { v };

            var permutatedValues =
                from a in new[] { null, "", "A", "A`", "Foo", "Foo```" }
                from b in new[] { null, "", "B", "B`", "Bar", "Bar```" }
                from c in new[] { null, "", "C", "C`", "Baz", "Baz```" }
                select new string[] { a, b, c }.Where(x => x != null).ToArray();

            return
                from v in Enumerable.Concat(singleValues, permutatedValues)
                from separator in new[] { "", ",", ", " }
                from lowerCase in new[] { true, false }
                select new object[]
                {
                    v,
                    separator,
                    lowerCase
                };
        }
    }
}
