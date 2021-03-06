﻿using System;
using System.Text;

namespace NanoSerializer
{
    internal static class Extensions
    {
        const int LengthSize = sizeof(ushort);

        internal static ushort ReadLength(this ref NanoReader reader)
        {
            var span = reader.Buffer.Slice(reader.Position, LengthSize);
            reader.Position += LengthSize;

            return BitConverter.ToUInt16(span);
        }

        internal static ReadOnlySpan<byte> Read(this ref NanoReader reader, int size)
        {
            var span = reader.Buffer.Slice(reader.Position, size);
            reader.Position += size;
            return span;
        }

        internal static byte[] ToBytes(this ReadOnlySpan<char> chars)
        {
            var bytesCount = Encoding.UTF8.GetByteCount(chars);
            Span<byte> span = stackalloc byte[bytesCount];
            Encoding.UTF8.GetBytes(chars, span);
            return span.ToArray();
        }

        internal static string ToText(this ReadOnlySpan<byte> span)
        {
            var charsCount = Encoding.UTF8.GetCharCount(span);
            Span<char> chars = stackalloc char[charsCount];
            Encoding.UTF8.GetChars(span, chars);
            return new string(chars);
        }
    }
}
