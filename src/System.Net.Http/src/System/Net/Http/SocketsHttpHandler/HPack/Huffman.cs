// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// See THIRD-PARTY-NOTICES.TXT in the project root for license information.

using System;
using System.Diagnostics;

namespace System.Net.Http.HPack
{
    internal class Huffman
    {
        private static readonly (uint code, int bitLength)[] _encodingTable = new (uint code, int bitLength)[]
        {
            (0b11111111_11000000_00000000_00000000, 13),
            (0b11111111_11111111_10110000_00000000, 23),
            (0b11111111_11111111_11111110_00100000, 28),
            (0b11111111_11111111_11111110_00110000, 28),
            (0b11111111_11111111_11111110_01000000, 28),
            (0b11111111_11111111_11111110_01010000, 28),
            (0b11111111_11111111_11111110_01100000, 28),
            (0b11111111_11111111_11111110_01110000, 28),
            (0b11111111_11111111_11111110_10000000, 28),
            (0b11111111_11111111_11101010_00000000, 24),
            (0b11111111_11111111_11111111_11110000, 30),
            (0b11111111_11111111_11111110_10010000, 28),
            (0b11111111_11111111_11111110_10100000, 28),
            (0b11111111_11111111_11111111_11110100, 30),
            (0b11111111_11111111_11111110_10110000, 28),
            (0b11111111_11111111_11111110_11000000, 28),
            (0b11111111_11111111_11111110_11010000, 28),
            (0b11111111_11111111_11111110_11100000, 28),
            (0b11111111_11111111_11111110_11110000, 28),
            (0b11111111_11111111_11111111_00000000, 28),
            (0b11111111_11111111_11111111_00010000, 28),
            (0b11111111_11111111_11111111_00100000, 28),
            (0b11111111_11111111_11111111_11111000, 30),
            (0b11111111_11111111_11111111_00110000, 28),
            (0b11111111_11111111_11111111_01000000, 28),
            (0b11111111_11111111_11111111_01010000, 28),
            (0b11111111_11111111_11111111_01100000, 28),
            (0b11111111_11111111_11111111_01110000, 28),
            (0b11111111_11111111_11111111_10000000, 28),
            (0b11111111_11111111_11111111_10010000, 28),
            (0b11111111_11111111_11111111_10100000, 28),
            (0b11111111_11111111_11111111_10110000, 28),
            (0b01010000_00000000_00000000_00000000, 6),
            (0b11111110_00000000_00000000_00000000, 10),
            (0b11111110_01000000_00000000_00000000, 10),
            (0b11111111_10100000_00000000_00000000, 12),
            (0b11111111_11001000_00000000_00000000, 13),
            (0b01010100_00000000_00000000_00000000, 6),
            (0b11111000_00000000_00000000_00000000, 8),
            (0b11111111_01000000_00000000_00000000, 11),
            (0b11111110_10000000_00000000_00000000, 10),
            (0b11111110_11000000_00000000_00000000, 10),
            (0b11111001_00000000_00000000_00000000, 8),
            (0b11111111_01100000_00000000_00000000, 11),
            (0b11111010_00000000_00000000_00000000, 8),
            (0b01011000_00000000_00000000_00000000, 6),
            (0b01011100_00000000_00000000_00000000, 6),
            (0b01100000_00000000_00000000_00000000, 6),
            (0b00000000_00000000_00000000_00000000, 5),
            (0b00001000_00000000_00000000_00000000, 5),
            (0b00010000_00000000_00000000_00000000, 5),
            (0b01100100_00000000_00000000_00000000, 6),
            (0b01101000_00000000_00000000_00000000, 6),
            (0b01101100_00000000_00000000_00000000, 6),
            (0b01110000_00000000_00000000_00000000, 6),
            (0b01110100_00000000_00000000_00000000, 6),
            (0b01111000_00000000_00000000_00000000, 6),
            (0b01111100_00000000_00000000_00000000, 6),
            (0b10111000_00000000_00000000_00000000, 7),
            (0b11111011_00000000_00000000_00000000, 8),
            (0b11111111_11111000_00000000_00000000, 15),
            (0b10000000_00000000_00000000_00000000, 6),
            (0b11111111_10110000_00000000_00000000, 12),
            (0b11111111_00000000_00000000_00000000, 10),
            (0b11111111_11010000_00000000_00000000, 13),
            (0b10000100_00000000_00000000_00000000, 6),
            (0b10111010_00000000_00000000_00000000, 7),
            (0b10111100_00000000_00000000_00000000, 7),
            (0b10111110_00000000_00000000_00000000, 7),
            (0b11000000_00000000_00000000_00000000, 7),
            (0b11000010_00000000_00000000_00000000, 7),
            (0b11000100_00000000_00000000_00000000, 7),
            (0b11000110_00000000_00000000_00000000, 7),
            (0b11001000_00000000_00000000_00000000, 7),
            (0b11001010_00000000_00000000_00000000, 7),
            (0b11001100_00000000_00000000_00000000, 7),
            (0b11001110_00000000_00000000_00000000, 7),
            (0b11010000_00000000_00000000_00000000, 7),
            (0b11010010_00000000_00000000_00000000, 7),
            (0b11010100_00000000_00000000_00000000, 7),
            (0b11010110_00000000_00000000_00000000, 7),
            (0b11011000_00000000_00000000_00000000, 7),
            (0b11011010_00000000_00000000_00000000, 7),
            (0b11011100_00000000_00000000_00000000, 7),
            (0b11011110_00000000_00000000_00000000, 7),
            (0b11100000_00000000_00000000_00000000, 7),
            (0b11100010_00000000_00000000_00000000, 7),
            (0b11100100_00000000_00000000_00000000, 7),
            (0b11111100_00000000_00000000_00000000, 8),
            (0b11100110_00000000_00000000_00000000, 7),
            (0b11111101_00000000_00000000_00000000, 8),
            (0b11111111_11011000_00000000_00000000, 13),
            (0b11111111_11111110_00000000_00000000, 19),
            (0b11111111_11100000_00000000_00000000, 13),
            (0b11111111_11110000_00000000_00000000, 14),
            (0b10001000_00000000_00000000_00000000, 6),
            (0b11111111_11111010_00000000_00000000, 15),
            (0b00011000_00000000_00000000_00000000, 5),
            (0b10001100_00000000_00000000_00000000, 6),
            (0b00100000_00000000_00000000_00000000, 5),
            (0b10010000_00000000_00000000_00000000, 6),
            (0b00101000_00000000_00000000_00000000, 5),
            (0b10010100_00000000_00000000_00000000, 6),
            (0b10011000_00000000_00000000_00000000, 6),
            (0b10011100_00000000_00000000_00000000, 6),
            (0b00110000_00000000_00000000_00000000, 5),
            (0b11101000_00000000_00000000_00000000, 7),
            (0b11101010_00000000_00000000_00000000, 7),
            (0b10100000_00000000_00000000_00000000, 6),
            (0b10100100_00000000_00000000_00000000, 6),
            (0b10101000_00000000_00000000_00000000, 6),
            (0b00111000_00000000_00000000_00000000, 5),
            (0b10101100_00000000_00000000_00000000, 6),
            (0b11101100_00000000_00000000_00000000, 7),
            (0b10110000_00000000_00000000_00000000, 6),
            (0b01000000_00000000_00000000_00000000, 5),
            (0b01001000_00000000_00000000_00000000, 5),
            (0b10110100_00000000_00000000_00000000, 6),
            (0b11101110_00000000_00000000_00000000, 7),
            (0b11110000_00000000_00000000_00000000, 7),
            (0b11110010_00000000_00000000_00000000, 7),
            (0b11110100_00000000_00000000_00000000, 7),
            (0b11110110_00000000_00000000_00000000, 7),
            (0b11111111_11111100_00000000_00000000, 15),
            (0b11111111_10000000_00000000_00000000, 11),
            (0b11111111_11110100_00000000_00000000, 14),
            (0b11111111_11101000_00000000_00000000, 13),
            (0b11111111_11111111_11111111_11000000, 28),
        };

        private static readonly (int codeLength, int[] codes)[] _decodingTable = new[]
        {
            (5, new[] { 48, 49, 50, 97, 99, 101, 105, 111, 115, 116 }),
            (6, new[] { 32, 37, 45, 46, 47, 51, 52, 53, 54, 55, 56, 57, 61, 65, 95, 98, 100, 102, 103, 104, 108, 109, 110, 112, 114, 117 }),
            (7, new[] { 58, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 89, 106, 107, 113, 118, 119, 120, 121, 122 }),
            (8, new[] { 38, 42, 44, 59, 88, 90 }),
            (10, new[] { 33, 34, 40, 41, 63 }),
            (11, new[] { 39, 43, 124 }),
            (12, new[] { 35, 62 }),
            (13, new[] { 0, 36, 64, 91, 93, 126 }),
            (14, new[] { 94, 125 }),
            (15, new[] { 60, 96, 123 }),
            (19, new[] { 92, 195, 208 }),
            (20, new[] { 128, 130, 131, 162, 184, 194, 224, 226 }),
            (21, new[] { 153, 161, 167, 172, 176, 177, 179, 209, 216, 217, 227, 229, 230 }),
            (22, new[] { 129, 132, 133, 134, 136, 146, 154, 156, 160, 163, 164, 169, 170, 173, 178, 181, 185, 186, 187, 189, 190, 196, 198, 228, 232, 233 }),
            (23, new[] { 1, 135, 137, 138, 139, 140, 141, 143, 147, 149, 150, 151, 152, 155, 157, 158, 165, 166, 168, 174, 175, 180, 182, 183, 188, 191, 197, 231, 239 }),
            (24, new[] { 9, 142, 144, 145, 148, 159, 171, 206, 215, 225, 236, 237 }),
            (25, new[] { 199, 207, 234, 235 }),
            (26, new[] { 192, 193, 200, 201, 202, 205, 210, 213, 218, 219, 238, 240, 242, 243, 255 }),
            (27, new[] { 203, 204, 211, 212, 214, 221, 222, 223, 241, 244, 245, 246, 247, 248, 250, 251, 252, 253, 254 }),
            (28, new[] { 2, 3, 4, 5, 6, 7, 8, 11, 12, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 127, 220, 249 }),
            (30, new[] { 10, 13, 22, 256 })
        };

        public static int GetByteCount(string src, bool lowerCase)
        {
            return (GetBitCount(src, lowerCase) + 7) / 8;
        }

        public static int GetByteCount(ReadOnlySpan<string> src, string separator, bool lowerCase)
        {
            if (src.Length == 0)
            {
                return 0;
            }

            int bits;

            checked
            {
                bits = GetBitCount(separator, lowerCase) * (src.Length - 1);

                foreach (string s in src)
                {
                    bits += GetBitCount(s, lowerCase);
                }
            }

            return (bits + 7) / 8;
        }

        private static int GetBitCount(string src, bool lowerCase)
        {
            int bits = 0;

            foreach (char ch in src)
            {
                if (ch > 127)
                {
                    throw new HttpRequestException(SR.net_http_request_invalid_char_encoding);
                }

                byte idx = lowerCase ? HPackEncoder.ToLowerAscii(ch) : (byte)ch;
                bits = checked(bits + _encodingTable[idx].bitLength);
            }

            return bits;
        }

        /// <summary>
        /// Huffman encodes header data, optionally as lower-case.
        /// </summary>
        /// <param name="src">The source string to encode.</param>
        /// <param name="lowerCase">If true, the string should be lower-cased. Header names should be lower-cased, while header values shouldn't be.</param>
        /// <param name="dst">The destination span to write the encoded value to.</param>
        /// <param name="bytesWritten">When the method returns true, receives the number of bytes written. Otherwise, the number of bytes required.</param>
        /// <returns>If true, <paramref name="src"/> was successfully Huffman encoded into <paramref name="dst"/>. Otherwise, <paramref name="dst"/> did not have enough space.</returns>
        public static bool TryEncode(string src, bool lowerCase, Span<byte> dst, out int bytesWritten)
        {
            Debug.Assert(src != null);

            ulong buffer = 0;
            int bufferLength = 0;
            int dstIdx = 0;

            (buffer, bufferLength, dstIdx) = EncodeHelper(src, lowerCase, dst, buffer, bufferLength, dstIdx);
            return FlushEncodeBuffer(dst, buffer, bufferLength, dstIdx, out bytesWritten);
        }

        /// <summary>
        /// Huffman encodes a header value.
        /// </summary>
        /// <param name="src">The source strings to concatenate and encode.</param>
        /// <param name="separator">A separator to concatenate between source strings.</param>
        /// <param name="lowerCase">If true, the string should be lower-cased. Header names should be lower-cased, while header values shouldn't be.</param>
        /// <param name="dst">The destination span to write the encoded value to.</param>
        /// <param name="bytesWritten">When the method returns true, receives the number of bytes written. Otherwise, the number of bytes required.</param>
        /// <returns>If true, <paramref name="src"/> was successfully Huffman encoded into <paramref name="dst"/>. Otherwise, <paramref name="dst"/> did not have enough space.</returns>
        public static bool TryEncode(ReadOnlyString<string> src, string separator, bool lowerCase, Span<byte> dst, out int bytesWritten)
        {
            Debug.Assert(src != null);
            Debug.Assert(separator != null);

            if (src.Length == 0)
            {
                bytesWritten = 0;
                return true;
            }

            ulong buffer = 0;
            int bufferLength = 0;
            int dstIdx = 0;

            (buffer, bufferLength, dstIdx) = EncodeHelper(src[0], lowerCase, dst, buffer, bufferLength, dstIdx);

            for (int i = 1; i < src.Length; ++i)
            {
                (buffer, bufferLength, dstIdx) = EncodeHelper(separator, lowerCase, dst, buffer, bufferLength, dstIdx);
                (buffer, bufferLength, dstIdx) = EncodeHelper(src[i], lowerCase, dst, buffer, bufferLength, dstIdx);
            }

            return FlushEncodeBuffer(dst, buffer, bufferLength, dstIdx, out bytesWritten);
        }

        private static (ulong buffer, int bufferLength, int dstIdx) EncodeHelper(string src, bool lowerCase, Span<byte> dst, ulong buffer, int bufferLength, int dstIdx)
        {
            foreach (char x in src)
            {
                if (x > 127)
                {
                    throw new HttpRequestException(SR.net_http_request_invalid_char_encoding);
                }

                byte idx = lowerCase ? HPackEncoder.ToLowerAscii(x) : (byte)x;

                (ulong code, int codeLength) = GetEncodedValue(idx);

                buffer |= code >> bufferLength;
                bufferLength += codeLength;

                while (bufferLength >= 8)
                {
                    if (dstIdx < dst.Length)
                    {
                        dst[dstIdx] = (byte)(buffer >> 56);
                    }

                    ++dstIdx;
                    buffer <<= 8;
                    bufferLength -= 8;
                }
            }

            return (buffer, bufferLength, dstIdx);
        }

        // Encoded values are 30 bits at most, so are stored in the table in a uint.
        // Convert to ulong here and put the encoded value in the most significant bits.
        // This makes the encoding logic below simpler.
        private static (ulong code, int bitLength) GetEncodedValue(byte b)
        {
            (uint code, int bitLength) = _encodingTable[b];
            return (((ulong)code) << 32, bitLength);
        }

        private static bool FlushEncodeBuffer(Span<byte> dst, ulong buffer, int bufferLength, int dstIdx, out int bytesWritten)
        {
            if (bufferLength != 0)
            {
                if (dstIdx < dst.Length)
                {
                    // Fill any trailing bits with ones, per RFC
                    buffer |= 0xFFFFFFFFFFFFFFFF >> bufferLength;

                    dst[dstIdx] = (byte)(buffer >> 56);
                }

                ++dstIdx;
            }

            bytesWritten = dstIdx;
            return dstIdx <= dst.Length;
        }

        /// <summary>
        /// Decodes a Huffman encoded string from a byte array.
        /// </summary>
        /// <param name="src">The source byte array containing the encoded data.</param>
        /// <param name="dstArray">The destination byte array to store the decoded data.  This may grow if its size is insufficient.</param>
        /// <returns>The number of decoded symbols.</returns>
        public static int Decode(ReadOnlySpan<byte> src, ref byte[] dstArray)
        {
            Span<byte> dst = dstArray;
            Debug.Assert(dst != null && dst.Length > 0);

            int i = 0;
            int j = 0;
            int lastDecodedBits = 0;
            while (i < src.Length)
            {
                // Note that if lastDecodeBits is 3 or more, then we will only get 5 bits (or less)
                // from src[i]. Thus we need to read 5 bytes here to ensure that we always have
                // at least 30 bits available for decoding.
                // TODO ISSUE 31751: Rework this as part of Huffman perf improvements
                uint next = (uint)(src[i] << 24 + lastDecodedBits);
                next |= (i + 1 < src.Length ? (uint)(src[i + 1] << 16 + lastDecodedBits) : 0);
                next |= (i + 2 < src.Length ? (uint)(src[i + 2] << 8 + lastDecodedBits) : 0);
                next |= (i + 3 < src.Length ? (uint)(src[i + 3] << lastDecodedBits) : 0);
                next |= (i + 4 < src.Length ? (uint)(src[i + 4] >> (8 - lastDecodedBits)) : 0);

                uint ones = (uint)(int.MinValue >> (8 - lastDecodedBits - 1));
                if (i == src.Length - 1 && lastDecodedBits > 0 && (next & ones) == ones)
                {
                    // The remaining 7 or less bits are all 1, which is padding.
                    // We specifically check that lastDecodedBits > 0 because padding
                    // longer than 7 bits should be treated as a decoding error.
                    // http://httpwg.org/specs/rfc7541.html#rfc.section.5.2
                    break;
                }

                // The longest possible symbol size is 30 bits. If we're at the last 4 bytes
                // of the input, we need to make sure we pass the correct number of valid bits
                // left, otherwise the trailing 0s in next may form a valid symbol.
                int validBits = Math.Min(30, (8 - lastDecodedBits) + (src.Length - i - 1) * 8);
                int ch = DecodeValue(next, validBits, out int decodedBits);

                if (ch == -1)
                {
                    // No valid symbol could be decoded with the bits in next
                    throw new HuffmanDecodingException();
                }
                else if (ch == 256)
                {
                    // A Huffman-encoded string literal containing the EOS symbol MUST be treated as a decoding error.
                    // http://httpwg.org/specs/rfc7541.html#rfc.section.5.2
                    throw new HuffmanDecodingException();
                }

                if (j == dst.Length)
                {
                    Array.Resize(ref dstArray, dst.Length * 2);
                    dst = dstArray;
                }

                dst[j++] = (byte)ch;

                // If we crossed a byte boundary, advance i so we start at the next byte that's not fully decoded.
                lastDecodedBits += decodedBits;
                i += lastDecodedBits / 8;

                // Modulo 8 since we only care about how many bits were decoded in the last byte that we processed.
                lastDecodedBits %= 8;
            }

            return j;
        }

        /// <summary>
        /// Decodes a single symbol from a 32-bit word.
        /// </summary>
        /// <param name="data">A 32-bit word containing a Huffman encoded symbol.</param>
        /// <param name="validBits">
        /// The number of bits in <paramref name="data"/> that may contain an encoded symbol.
        /// This is not the exact number of bits that encode the symbol. Instead, it prevents
        /// decoding the lower bits of <paramref name="data"/> if they don't contain any
        /// encoded data.
        /// </param>
        /// <param name="decodedBits">The number of bits decoded from <paramref name="data"/>.</param>
        /// <returns>The decoded symbol.</returns>
        internal static int DecodeValue(uint data, int validBits, out int decodedBits)
        {
            // The code below implements the decoding logic for a canonical Huffman code.
            //
            // To decode a symbol, we scan the decoding table, which is sorted by ascending symbol bit length.
            // For each bit length b, we determine the maximum b-bit encoded value, plus one (that is codeMax).
            // This is done with the following logic:
            //
            // if we're at the first entry in the table,
            //    codeMax = the # of symbols encoded in b bits
            // else,
            //    left-shift codeMax by the difference between b and the previous entry's bit length,
            //    then increment codeMax by the # of symbols encoded in b bits
            //
            // Next, we look at the value v encoded in the highest b bits of data. If v is less than codeMax,
            // those bits correspond to a Huffman encoded symbol. We find the corresponding decoded
            // symbol in the list of values associated with bit length b in the decoding table by indexing it
            // with codeMax - v.

            int codeMax = 0;

            for (int i = 0; i < _decodingTable.Length && _decodingTable[i].codeLength <= validBits; i++)
            {
                (int codeLength, int[] codes) = _decodingTable[i];

                if (i > 0)
                {
                    codeMax <<= codeLength - _decodingTable[i - 1].codeLength;
                }

                codeMax += codes.Length;

                int mask = int.MinValue >> (codeLength - 1);
                long masked = (data & mask) >> (32 - codeLength);

                if (masked < codeMax)
                {
                    decodedBits = codeLength;
                    return codes[codes.Length - (codeMax - masked)];
                }
            }

            decodedBits = 0;
            return -1;
        }
    }
}
