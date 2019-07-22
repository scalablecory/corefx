using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Policy;
using System.Text;

namespace System.Net.Test.Common
{

    public sealed class HPackEncoder
    {
        private Dictionary<TableEntry, int> _dynamicTableMap = new Dictionary<TableEntry, int>();
        private TableEntry[] _dynamicTable = new TableEntry[32];
        private int _dynamicHead, _dynamicCount, _dynamicSize, _dynamicMaxSize;

        private void AddDynamicEntry(TableEntry entry)
        {
            int entrySize = entry.DynamicSize;

            if ((_dynamicMaxSize - _dynamicSize) < entrySize)
            {
                EnsureAvailable(entrySize);
            }

            if (_dynamicCount == _dynamicTable.Length)
            {
                ResizeDynamicTable();
            }

            int insertIndex = (_dynamicHead + _dynamicCount) & (_dynamicTable.Length - 1);
            _dynamicTable[insertIndex] = entry;

            _dynamicSize += entrySize;
            _dynamicHead++;
            _dynamicCount++;

            _dynamicTableMap.Add(entry, insertIndex);
        }

        private void ResizeDynamicTable()
        {
            var newEntries = new TableEntry[_dynamicCount * 2];

            int countA = _dynamicCount - _dynamicHead;
            int countB = _dynamicCount - countA;

            Array.Copy(_dynamicTable, _dynamicHead, newEntries, 0, countA);
            Array.Copy(_dynamicTable, 0, newEntries, countA, countB);

            _dynamicTable = newEntries;
            _dynamicHead = 0;

            for (int i = 0; i < _dynamicCount; ++i)
            {
                _dynamicTableMap[_dynamicTable[i]] = i;
            }
        }

        private void EnsureAvailable(int size)
        {
            Debug.Assert(size >= 0);

            do
            {
                ref TableEntry e = ref _dynamicTable[_dynamicHead];

                _dynamicSize -= e.DynamicSize;
                _dynamicTableMap.Remove(e);
                e = default;

                _dynamicHead = (_dynamicHead + 1) & (_dynamicTable.Length - 1);
                _dynamicCount--;
            }
            while ((_dynamicMaxSize - _dynamicSize) < size);
        }

        public bool TryGetEntryIndex(string name, string value, out int index)
        {
            int staticIdx = Array.BinarySearch(s_staticTable, new TableEntry(name, value));

            if (staticIdx >= 0)
            {
                index = staticIdx;
                return true;
            }

            if (!_dynamicTableMap.TryGetValue(new TableEntry(name, value), out int dynamicIdx))
            {
                index = default;
                return false;
            }

            index = dynamicIdx;
            return true;
        }

        public static int EncodeHeader(int headerIndex, Span<byte> headerBlock)
        {
            return EncodeInteger(headerIndex, 0b10000000, 0b10000000, headerBlock);
        }

        public static int EncodeHeader(int nameIdx, string value, HPackFlags flags, Span<byte> headerBlock)
        {
            return EncodeHeaderImpl(nameIdx, null, value, flags, headerBlock);
        }

        public static int EncodeHeader(string name, string value, HPackFlags flags, Span<byte> headerBlock)
        {
            return EncodeHeaderImpl(0, name, value, flags, headerBlock);
        }

        static int EncodeHeaderImpl(int nameIdx, string name, string value, HPackFlags flags, Span<byte> headerBlock)
        {
            byte prefix, prefixMask;

            switch (flags & HPackFlags.MaskIndexing)
            {
                case HPackFlags.WithoutIndexing:
                    prefix = 0;
                    prefixMask = 0b11110000;
                    break;
                case HPackFlags.NewIndexed:
                    prefix = 0b01000000;
                    prefixMask = 0b11000000;
                    break;
                case HPackFlags.NeverIndexed:
                    prefix = 0b00010000;
                    prefixMask = 0b11110000;
                    break;
                default:
                    throw new Exception("invalid indexing flag");
            }

            int bytesGenerated = EncodeInteger(nameIdx, prefix, prefixMask, headerBlock);

            if (name != null)
            {
                bytesGenerated += EncodeString(name, (flags & HPackFlags.HuffmanEncodeName) != 0, headerBlock.Slice(bytesGenerated));
            }

            bytesGenerated += EncodeString(value, (flags & HPackFlags.HuffmanEncodeValue) != 0, headerBlock.Slice(bytesGenerated));
            return bytesGenerated;
        }

        public static int EncodeDynamicTableSizeUpdate(int maximumSize, Span<byte> headerBlock)
        {
            return EncodeInteger(maximumSize, 0b00100000, 0b11100000, headerBlock);
        }

        public static int EncodeString(string value, bool huffmanEncode, Span<byte> headerBlock)
        {
            byte[] data = Encoding.ASCII.GetBytes(value);
            byte prefix;

            if (!huffmanEncode)
            {
                prefix = 0;
            }
            else
            {
                int len = HuffmanEncoder.GetEncodedLength(data);

                byte[] huffmanData = new byte[len];
                HuffmanEncoder.Encode(data, huffmanData);

                data = huffmanData;
                prefix = 0x80;
            }

            int bytesGenerated = 0;

            bytesGenerated += EncodeInteger(data.Length, prefix, 0x80, headerBlock);

            data.AsSpan().CopyTo(headerBlock.Slice(bytesGenerated));
            bytesGenerated += data.Length;

            return bytesGenerated;
        }

        public static int EncodeInteger(int value, byte prefix, byte prefixMask, Span<byte> headerBlock)
        {
            byte prefixLimit = (byte)(~prefixMask);

            if (value < prefixLimit)
            {
                headerBlock[0] = (byte)(prefix | value);
                return 1;
            }

            headerBlock[0] = (byte)(prefix | prefixLimit);
            int bytesGenerated = 1;

            value -= prefixLimit;

            while (value >= 0x80)
            {
                headerBlock[bytesGenerated] = (byte)((value & 0x7F) | 0x80);
                value = value >> 7;
                bytesGenerated++;
            }

            headerBlock[bytesGenerated] = (byte)value;
            bytesGenerated++;

            return bytesGenerated;
        }

        private static readonly TableEntry[] s_staticTable = new TableEntry[]
        {
            new TableEntry(":authority", ""),
            new TableEntry(":method", "GET"),
            new TableEntry(":method", "POST"),
            new TableEntry(":path", "/"),
            new TableEntry(":path", "/index.html"),
            new TableEntry(":scheme", "http"),
            new TableEntry(":scheme", "https"),
            new TableEntry(":status", "200"),
            new TableEntry(":status", "204"),
            new TableEntry(":status", "206"),
            new TableEntry(":status", "304"),
            new TableEntry(":status", "400"),
            new TableEntry(":status", "404"),
            new TableEntry(":status", "500"),
            new TableEntry("accept-charset", ""),
            new TableEntry("accept-encoding", "gzip, deflate"),
            new TableEntry("accept-language", ""),
            new TableEntry("accept-ranges", ""),
            new TableEntry("accept", ""),
            new TableEntry("access-control-allow-origin", ""),
            new TableEntry("age", ""),
            new TableEntry("allow", ""),
            new TableEntry("authorization", ""),
            new TableEntry("cache-control", ""),
            new TableEntry("content-disposition", ""),
            new TableEntry("content-encoding", ""),
            new TableEntry("content-language", ""),
            new TableEntry("content-length", ""),
            new TableEntry("content-location", ""),
            new TableEntry("content-range", ""),
            new TableEntry("content-type", ""),
            new TableEntry("cookie", ""),
            new TableEntry("date", ""),
            new TableEntry("etag", ""),
            new TableEntry("expect", ""),
            new TableEntry("expires", ""),
            new TableEntry("from", ""),
            new TableEntry("host", ""),
            new TableEntry("if-match", ""),
            new TableEntry("if-modified-since", ""),
            new TableEntry("if-none-match", ""),
            new TableEntry("if-range", ""),
            new TableEntry("if-unmodified-since", ""),
            new TableEntry("last-modified", ""),
            new TableEntry("link", ""),
            new TableEntry("location", ""),
            new TableEntry("max-forwards", ""),
            new TableEntry("proxy-authenticate", ""),
            new TableEntry("proxy-authorization", ""),
            new TableEntry("range", ""),
            new TableEntry("referer", ""),
            new TableEntry("refresh", ""),
            new TableEntry("retry-after", ""),
            new TableEntry("server", ""),
            new TableEntry("set-cookie", ""),
            new TableEntry("strict-transport-security", ""),
            new TableEntry("transfer-encoding", ""),
            new TableEntry("user-agent", ""),
            new TableEntry("vary", ""),
            new TableEntry("via", ""),
            new TableEntry("www-authenticate", "")
        };

        struct TableEntry : IComparable<TableEntry>, IEquatable<TableEntry>
        {
            private const int DynamicOverhead = 32;

            public string Name { get; }
            public string Value { get; }
            public int DynamicSize => Name.Length + Value.Length + DynamicOverhead;

            public TableEntry(string name, string value)
            {
                Name = name;
                Value = value;
            }

            public int CompareTo(TableEntry other)
            {
                int c = string.Compare(Name, other.Name, StringComparison.Ordinal);
                if (c != 0) return c;

                return string.Compare(Value, other.Value, StringComparison.Ordinal);
            }

            public bool Equals(TableEntry other)
            {
                return string.Equals(Name, other.Name, StringComparison.Ordinal) && string.Equals(Value, other.Value, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is TableEntry e && Equals(e);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Value);
            }
        }
    }

    [Flags]
    public enum HPackFlags
    {
        HuffmanEncodeName = 1,
        HuffmanEncodeValue = 2,
        HuffmanEncode = HuffmanEncodeName | HuffmanEncodeValue,

        WithoutIndexing = 0,
        NewIndexed = 4,
        NeverIndexed = 8,
        MaskIndexing = WithoutIndexing | NewIndexed | NeverIndexed
    }

}
