using System.Text;

namespace D4LootBench.Core.Codec;

internal static class ProtoWriter
{
    internal static byte[] Varint(ulong value)
    {
        var bytes = new List<byte>(10);
        do
        {
            var b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            bytes.Add(b);
        } while (value != 0);
        return [.. bytes];
    }

    internal static byte[] Fixed32(uint value) =>
    [
        (byte)(value & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 24) & 0xFF)
    ];

    internal static byte[] VarintField(int fieldNumber, ulong value) =>
        [.. Tag(fieldNumber, 0), .. Varint(value)];

    internal static byte[] Fixed32Field(int fieldNumber, uint value) =>
        [.. Tag(fieldNumber, 5), .. Fixed32(value)];

    internal static byte[] LenField(int fieldNumber, byte[] data) =>
        [.. Tag(fieldNumber, 2), .. Varint((ulong)data.Length), .. data];

    internal static byte[] StringField(int fieldNumber, string value) =>
        LenField(fieldNumber, Encoding.UTF8.GetBytes(value));

    private static byte[] Tag(int fieldNumber, int wireType) =>
        Varint((ulong)((fieldNumber << 3) | wireType));
}
