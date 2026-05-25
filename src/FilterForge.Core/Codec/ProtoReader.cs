using System.Text;

namespace ThunderEagle.FilterForge.Core.Codec;

internal sealed class ProtoReader
{
    private readonly byte[] _data;
    private int _pos;

    internal ProtoReader(byte[] data) { _data = data; _pos = 0; }

    internal bool HasData => _pos < _data.Length;
    internal int Position => _pos;

    internal void Seek(int position) => _pos = position;

    internal (int FieldNumber, int WireType) ReadTag()
    {
        var tag = (int)ReadVarint();
        return (tag >> 3, tag & 0x07);
    }

    internal ulong ReadVarint()
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            var b = _data[_pos++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
        }
    }

    internal uint ReadFixed32()
    {
        var v = (uint)(_data[_pos]
            | (_data[_pos + 1] << 8)
            | (_data[_pos + 2] << 16)
            | (_data[_pos + 3] << 24));
        _pos += 4;
        return v;
    }

    internal byte[] ReadLenBytes()
    {
        var len = (int)ReadVarint();
        var bytes = new byte[len];
        Array.Copy(_data, _pos, bytes, 0, len);
        _pos += len;
        return bytes;
    }

    internal string ReadString() => Encoding.UTF8.GetString(ReadLenBytes());

    internal void Skip(int wireType)
    {
        switch (wireType)
        {
            case 0: ReadVarint(); break;
            case 1: _pos += 8; break;
            case 2: ReadLenBytes(); break;
            case 3: case 4: break; // deprecated Start/End group — ignore
            case 5: _pos += 4; break;
            default: break; // unknown wire type — skip
        }
    }
}
