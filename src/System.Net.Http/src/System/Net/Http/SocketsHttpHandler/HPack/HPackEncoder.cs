// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
// See THIRD-PARTY-NOTICES.TXT in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace System.Net.Http.HPack
{
    internal class HPackEncoder
    {
        private const int HuffmanOptimisticCutoff = 128;
        private const int SingleByteOverheadStringLiteralMaxLength = 126;

        private IEnumerator<KeyValuePair<string, string>> _enumerator;

        public bool BeginEncode(IEnumerable<KeyValuePair<string, string>> headers, Span<byte> buffer, out int length)
        {
            _enumerator = headers.GetEnumerator();
            _enumerator.MoveNext();

            return Encode(buffer, out length);
        }

        public bool BeginEncode(int statusCode, IEnumerable<KeyValuePair<string, string>> headers, Span<byte> buffer, out int length)
        {
            _enumerator = headers.GetEnumerator();
            _enumerator.MoveNext();

            int statusCodeLength = EncodeStatusCode(statusCode, buffer);
            bool done = Encode(buffer.Slice(statusCodeLength), throwIfNoneEncoded: false, out int headersLength);
            length = statusCodeLength + headersLength;

            return done;
        }

        public bool Encode(Span<byte> buffer, out int length)
        {
            return Encode(buffer, throwIfNoneEncoded: true, out length);
        }

        private bool Encode(Span<byte> buffer, bool throwIfNoneEncoded, out int length)
        {
            int currentLength = 0;
            do
            {
                if (!EncodeHeader(_enumerator.Current.Key, _enumerator.Current.Value, buffer.Slice(currentLength), out int headerLength))
                {
                    if (currentLength == 0 && throwIfNoneEncoded)
                    {
                        throw new HPackEncodingException();
                    }

                    length = currentLength;
                    return false;
                }

                currentLength += headerLength;
            }
            while (_enumerator.MoveNext());

            length = currentLength;

            return true;
        }

        private int EncodeStatusCode(int statusCode, Span<byte> buffer)
        {
            switch (statusCode)
            {
                // Status codes which exist in the HTTP/2 StaticTable.
                case 200:
                case 204:
                case 206:
                case 304:
                case 400:
                case 404:
                case 500:
                    buffer[0] = (byte)(0x80 | StaticTable.StatusIndex[statusCode]);
                    return 1;
                default:
                    // Send as Literal Header Field Without Indexing - Indexed Name
                    buffer[0] = 0x08;

                    ReadOnlySpan<byte> statusBytes = StatusCodes.ToStatusBytes(statusCode);
                    buffer[1] = (byte)statusBytes.Length;
                    statusBytes.CopyTo(buffer.Slice(2));

                    return 2 + statusBytes.Length;
            }
        }

        private bool EncodeHeader(string name, string value, Span<byte> buffer, out int length)
        {
            int i = 0;
            length = 0;

            if (buffer.Length == 0)
            {
                return false;
            }

            buffer[i++] = 0;

            if (i == buffer.Length)
            {
                return false;
            }

            if (!EncodeString(name, buffer.Slice(i), out int nameLength, lowercase: true))
            {
                return false;
            }

            i += nameLength;

            if (i >= buffer.Length)
            {
                return false;
            }

            if (!EncodeString(value, buffer.Slice(i), out int valueLength, lowercase: false))
            {
                return false;
            }

            i += valueLength;

            length = i;
            return true;
        }

        private bool EncodeString(string value, Span<byte> destination, out int bytesWritten, bool lowercase)
        {
            // From https://tools.ietf.org/html/rfc7541#section-5.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | H |    String Length (7+)     |
            // +---+---------------------------+
            // |  String Data (Length octets)  |
            // +-------------------------------+

            if (destination.Length != 0)
            {
                destination[0] = 0; // TODO: Use Huffman encoding
                if (IntegerEncoder.Encode(value.Length, 7, destination, out int integerLength))
                {
                    Debug.Assert(integerLength >= 1);

                    destination = destination.Slice(integerLength);
                    if (value.Length <= destination.Length)
                    {
                        for (int i = 0; i < value.Length; i++)
                        {
                            char c = value[i];
                            destination[i] = (byte)((uint)(c - 'A') <= ('Z' - 'A') ? c | 0x20 : c);
                        }

                        bytesWritten = integerLength + value.Length;
                        return true;
                    }
                }
            }

            bytesWritten = 0;
            return false;
        }

        // Things we should add:
        // * Huffman encoding
        //
        // Things we should consider adding:
        // * Dynamic table encoding:
        //   This would make the encoder stateful, which complicates things significantly.
        //   Additionally, it's not clear exactly what strings we would add to the dynamic table
        //   without some additional guidance from the user about this.
        //   So for now, don't do dynamic encoding.

        /// <summary>Encodes an "Indexed Header Field".</summary>
        public static bool EncodeIndexedHeaderField(int index, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-6.1
            // ----------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | 1 |        Index (7+)         |
            // +---+---------------------------+

            if (destination.Length != 0)
            {
                destination[0] = 0x80;
                return IntegerEncoder.Encode(index, 7, destination, out bytesWritten);
            }

            bytesWritten = 0;
            return false;
        }

        /// <summary>Encodes a "Literal Header Field without Indexing".</summary>
        public static bool EncodeLiteralHeaderFieldWithoutIndexing(int index, string value, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-6.2.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | 0 | 0 | 0 | 0 |  Index (4+)   |
            // +---+---+-----------------------+
            // | H |     Value Length (7+)     |
            // +---+---------------------------+
            // | Value String (Length octets)  |
            // +-------------------------------+

            if ((uint)destination.Length >= 2)
            {
                destination[0] = 0;
                if (IntegerEncoder.Encode(index, 4, destination, out int indexLength))
                {
                    Debug.Assert(indexLength >= 1);
                    if (EncodeStringLiteral(value, lowerCase: false, destination.Slice(indexLength), out int nameLength))
                    {
                        bytesWritten = indexLength + nameLength;
                        return true;
                    }
                }
            }

            bytesWritten = 0;
            return false;
        }

        /// <summary>
        /// Encodes a "Literal Header Field without Indexing", but only the index portion;
        /// a subsequent call to <see cref="EncodeStringLiteral"/> must be used to encode the associated value.
        /// </summary>
        private static bool EncodeLiteralHeaderFieldWithoutIndexing(int index, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-6.2.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | 0 | 0 | 0 | 0 |  Index (4+)   |
            // +---+---+-----------------------+
            //
            // ... expected after this:
            //
            // | H |     Value Length (7+)     |
            // +---+---------------------------+
            // | Value String (Length octets)  |
            // +-------------------------------+

            if ((uint)destination.Length != 0)
            {
                destination[0] = 0;
                if (IntegerEncoder.Encode(index, 4, destination, out int indexLength))
                {
                    Debug.Assert(indexLength >= 1);
                    bytesWritten = indexLength;
                    return true;
                }
            }

            bytesWritten = 0;
            return false;
        }

        /// <summary>Encodes a "Literal Header Field without Indexing - New Name".</summary>
        public static bool EncodeLiteralHeaderFieldWithoutIndexingNewName(string name, ReadOnlySpan<string> values, string separator, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-6.2.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | 0 | 0 | 0 | 0 |       0       |
            // +---+---+-----------------------+
            // | H |     Name Length (7+)      |
            // +---+---------------------------+
            // |  Name String (Length octets)  |
            // +---+---------------------------+
            // | H |     Value Length (7+)     |
            // +---+---------------------------+
            // | Value String (Length octets)  |
            // +-------------------------------+

            if ((uint)destination.Length >= 3)
            {
                destination[0] = 0;
                if (EncodeStringLiteral(name, lowerCase: true, destination.Slice(1), out int nameLength) &&
                    EncodeStringLiterals(values, separator, destination.Slice(1 + nameLength), out int valueLength))
                {
                    bytesWritten = 1 + nameLength + valueLength;
                    return true;
                }
            }

            bytesWritten = 0;
            return false;
        }

        /// <summary>
        /// Encodes a "Literal Header Field without Indexing - New Name", but only the name portion;
        /// a subsequent call to <see cref="EncodeStringLiteral"/> must be used to encode the associated value.
        /// </summary>
        private static bool EncodeLiteralHeaderFieldWithoutIndexingNewName(string name, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-6.2.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | 0 | 0 | 0 | 0 |       0       |
            // +---+---+-----------------------+
            // | H |     Name Length (7+)      |
            // +---+---------------------------+
            // |  Name String (Length octets)  |
            // +---+---------------------------+
            //
            // ... expected after this:
            //
            // | H |     Value Length (7+)     |
            // +---+---------------------------+
            // | Value String (Length octets)  |
            // +-------------------------------+

            if ((uint)destination.Length != 0)
            {
                destination[0] = 0;
                if (EncodeStringLiteral(name, lowerCase: true, destination.Slice(1), out int nameLength))
                {
                    bytesWritten = 1 + nameLength;
                    return true;
                }
            }

            bytesWritten = 0;
            return false;
        }

        /// <summary>
        /// Determines the buffer size for optimistic Huffman encoding.
        /// If the Huffman encoder needs more than this, the value either won't fit in the buffer or won't fit within a single length byte.
        /// </summary>
        private static int GetOptimisticHuffmanBufferSize(int valueLength, int destinationLength)
        {
            Debug.Assert(valueLength >= 0);
            Debug.Assert(destinationLength > 0);

            int optimisticBufferLength = destinationLength - 1;
            optimisticBufferLength = Math.Clamp(valueLength - 1, 0, optimisticBufferLength);
            optimisticBufferLength = Math.Min(optimisticBufferLength, SingleByteOverheadStringLiteralMaxLength);
            return optimisticBufferLength;
        }

        public static bool EncodeStringLiteral(string value, bool lowerCase, Span<byte> destination, out int bytesWritten)
        {
            // From https://tools.ietf.org/html/rfc7541#section-5.2
            // ------------------------------------------------------
            //   0   1   2   3   4   5   6   7
            // +---+---+---+---+---+---+---+---+
            // | H |    String Length (7+)     |
            // +---+---------------------------+
            // |  String Data (Length octets)  |
            // +-------------------------------+

            if (destination.Length == 0)
            {
                bytesWritten = 0;
                return false;
            }

            int integerLength, huffmanLength;

            if (value.Length < HuffmanOptimisticCutoff)
            {
                int optimisticBufferLength = GetOptimisticHuffmanBufferSize(value.Length, destination.Length);

                if (Huffman.TryEncode(value, lowerCase, destination.Slice(1, optimisticBufferLength), out huffmanLength))
                {
                    Debug.Assert(huffmanLength == Huffman.GetByteCount(value, lowerCase));

                    // Successfully encoded optimistically, so we only have a 1-byte prefix.
                    destination[0] = 0x80;
                    bool integerSuccess = IntegerEncoder.Encode(huffmanLength, 7, destination, out integerLength);
                    Debug.Assert(integerSuccess == true && integerLength == 1);

                    bytesWritten = integerLength + huffmanLength;
                    return true;
                }
            }
            else
            {
                huffmanLength = Huffman.GetByteCount(value, lowerCase);
            }

            if (huffmanLength < value.Length)
            {
                // Large value found. It may need mroe than one byte to encode its length.

                destination[0] = 0x80;
                if (!IntegerEncoder.Encode(huffmanLength, 7, destination, out integerLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                Debug.Assert(integerLength >= 1);

                if (Huffman.TryEncode(value, lowerCase, destination.Slice(integerLength), out int actualHuffmanLength))
                {
                    Debug.Assert(actualHuffmanLength == huffmanLength);

                    bytesWritten = integerLength + actualHuffmanLength;
                    return true;
                }

                bytesWritten = 0;
                return false;
            }

            // Huffman would expand our data. Write it out uncompressed.

            destination[0] = 0;
            if (!IntegerEncoder.Encode(value.Length, 7, destination, out integerLength))
            {
                bytesWritten = 0;
                return false;
            }

            Debug.Assert(integerLength >= 1);

            destination = destination.Slice(integerLength);
            if (value.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            EncodeUncompressedStringLiteralPart(value, lowerCase, destination);

            bytesWritten = integerLength + value.Length;
            return true;
        }

        /// <summary>
        /// Encodes a string literal, including envelope. Concatenates multiple values with separators in between. Huffman-encodes if it would save space.
        /// </summary>
        public static bool EncodeStringLiterals(ReadOnlySpan<string> values, string separator, Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;

            if (values.Length == 0)
            {
                return EncodeStringLiteral("", lowerCase: false, destination, out bytesWritten);
            }
            else if (values.Length == 1)
            {
                return EncodeStringLiteral(values[0], lowerCase: false, destination, out bytesWritten);
            }

            if (destination.Length == 0)
            {
                return false;
            }

            int valueLength;

            // Calculate length of all parts and separators.
            checked
            {
                valueLength = (values.Length - 1) * separator.Length;
                foreach (string valuePart in values)
                {
                    valueLength += valuePart.Length;
                }
            }

            int integerLength, huffmanLength;

            if (valueLength < HuffmanOptimisticCutoff)
            {
                int optimisticBufferLength = GetOptimisticHuffmanBufferSize(valueLength, destination.Length);

                if (Huffman.TryEncode(values, separator, lowerCase: false, destination.Slice(1, optimisticBufferLength), out huffmanLength))
                {
                    Debug.Assert(huffmanLength == Huffman.GetByteCount(values, separator, lowerCase: false));

                    // Successfully encoded optimistically. Write out a single-byte length prefix.
                    destination[0] = 0x80;
                    bool integerSuccess = IntegerEncoder.Encode(huffmanLength, 7, destination, out integerLength);
                    Debug.Assert(integerSuccess == true && integerLength == 1);

                    bytesWritten = integerLength + huffmanLength;
                    return true;
                }
            }
            else
            {
                huffmanLength = Huffman.GetByteCount(values, separator, lowerCase: false);
            }

            if (huffmanLength < valueLength)
            {
                // Large value found. Length prefix may be more than one byte.
                destination[0] = 0x80;
                if (!IntegerEncoder.Encode(huffmanLength, 7, destination, out integerLength))
                {
                    bytesWritten = 0;
                    return false;
                }

                Debug.Assert(integerLength >= 1);

                if (Huffman.TryEncode(values, separator, lowerCase: false, destination.Slice(integerLength), out int actualHuffmanLength))
                {
                    Debug.Assert(actualHuffmanLength == huffmanLength);
                    bytesWritten = integerLength + actualHuffmanLength;
                    return true;
                }

                bytesWritten = 0;
                return false;
            }

            // Huffman would expand our data. Write it out uncompressed.
            destination[0] = 0;
            if (!IntegerEncoder.Encode(valueLength, 7, destination, out integerLength))
            {
                bytesWritten = 0;
                return false;
            }

            Debug.Assert(integerLength >= 1);

            destination = destination.Slice(integerLength);
            if (valueLength > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            string part = values[0];
            EncodeUncompressedStringLiteralPart(part, lowerCase: false, destination);
            int offset = part.Length;

            for (int i = 1; i < values.Length; ++i)
            {
                EncodeUncompressedStringLiteralPart(separator, lowerCase: false, destination.Slice(offset));
                offset += separator.Length;

                part = values[i];
                EncodeUncompressedStringLiteralPart(part, lowerCase: false, destination.Slice(offset));
                offset += part.Length;
            }

            Debug.Assert(offset == valueLength);

            bytesWritten = integerLength + offset;
            return true;
        }

        private static void EncodeUncompressedStringLiteralPart(string value, bool lowerCase, Span<byte> dst)
        {
            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];
                Debug.Assert(c <= 127, $"{nameof(Headers.HttpRequestHeaders)} should have prevented non-ASCII headers.");

                dst[i] = lowerCase ? ToLowerAscii(c) : (byte)c;
            }
        }

        /// <summary>
        /// Encodes a "Literal Header Field without Indexing" to a new array, but only the index portion;
        /// a subsequent call to <see cref="EncodeStringLiteral"/> must be used to encode the associated value.
        /// </summary>
        public static byte[] EncodeLiteralHeaderFieldWithoutIndexingToAllocatedArray(int index)
        {
            Span<byte> span = stackalloc byte[256];
            bool success = EncodeLiteralHeaderFieldWithoutIndexing(index, span, out int length);
            Debug.Assert(success, $"Stack-allocated space was too small for index '{index}'.");
            return span.Slice(0, length).ToArray();
        }

        /// <summary>
        /// Encodes a "Literal Header Field without Indexing - New Name" to a new array, but only the name portion;
        /// a subsequent call to <see cref="EncodeStringLiteral"/> must be used to encode the associated value.
        /// </summary>
        public static byte[] EncodeLiteralHeaderFieldWithoutIndexingNewNameToAllocatedArray(string name)
        {
            Span<byte> span = stackalloc byte[256];
            bool success = EncodeLiteralHeaderFieldWithoutIndexingNewName(name, span, out int length);
            Debug.Assert(success, $"Stack-allocated space was too small for \"{name}\".");
            return span.Slice(0, length).ToArray();
        }

        /// <summary>Encodes a "Literal Header Field without Indexing" to a new array.</summary>
        public static byte[] EncodeLiteralHeaderFieldWithoutIndexingToAllocatedArray(int index, string value)
        {
            Span<byte> span =
#if DEBUG
                stackalloc byte[4]; // to validate growth algorithm
#else
                stackalloc byte[512];
#endif
            while (true)
            {
                if (EncodeLiteralHeaderFieldWithoutIndexing(index, value, span, out int length))
                {
                    return span.Slice(0, length).ToArray();
                }

                // This is a rare path, only used once per HTTP/2 connection and only
                // for very long host names.  Just allocate rather than complicate
                // the code with ArrayPool usage.  In practice we should never hit this,
                // as hostnames should be <= 255 characters.
                span = new byte[span.Length * 2];
            }
        }

        public static byte ToLowerAscii(char c)
        {
            return (byte)((uint)(c - 'A') <= ('Z' - 'A') ? c | 0x20 : c);
        }
    }
}
