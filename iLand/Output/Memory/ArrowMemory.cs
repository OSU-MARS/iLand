using Apache.Arrow;
using Apache.Arrow.Types;
using System;
using System.Runtime.InteropServices;

namespace iLand.Output.Memory
{
    internal class ArrowMemory
    {
        protected int Count { get; set; }

        protected ArrowMemory()
        {
            this.Count = 0;
        }

        // provide CopyFirstN() overloads specialized to source type
        // While CopyFirstN<TSource>() is cleaner at the member function level it's more complex for callers as often TSource
        // cannot be inferred.
        protected void CopyFirstN(ReadOnlySpan<float> source, Memory<byte> field, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, float>(field.Span).Slice(this.Count, count));
        }

        protected void CopyFirstN(ReadOnlySpan<Int16> source, Memory<byte> field, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(this.Count, count));
        }

        protected void CopyFirstN(ReadOnlySpan<Int32> source, Memory<byte> field, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(this.Count, count));
        }

        protected void CopyFirstN(ReadOnlySpan<UInt16> source, Memory<byte> field, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, UInt16>(field.Span).Slice(this.Count, count));
        }

        protected void CopyFirstN(ReadOnlySpan<UInt32> source, Memory<byte> field, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(this.Count, count));
        }

        protected void Fill(Memory<byte> field, IntegerType fieldType, Int32 value, int count)
        {
            switch (fieldType.BitWidth)
            {
                case 8:
                    MemoryMarshal.Cast<byte, sbyte>(field.Span).Slice(this.Count, count).Fill((sbyte)value);
                    break;
                case 16:
                    MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(this.Count, count).Fill((Int16)value);
                    break;
                case 32:
                    MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(this.Count, count).Fill(value);
                    break;
                case 64:
                    MemoryMarshal.Cast<byte, Int64>(field.Span).Slice(this.Count, count).Fill((Int64)value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldType));
            }
        }

        protected void Fill(Memory<byte> field, IntegerType fieldType, UInt32 value, int count)
        {
            switch (fieldType.BitWidth)
            {
                case 8:
                    MemoryMarshal.Cast<byte, byte>(field.Span).Slice(this.Count, count).Fill((byte)value);
                    break;
                case 16:
                    MemoryMarshal.Cast<byte, UInt16>(field.Span).Slice(this.Count, count).Fill((UInt16)value);
                    break;
                case 32:
                    MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(this.Count, count).Fill((UInt32)value);
                    break;
                case 64:
                    MemoryMarshal.Cast<byte, UInt64>(field.Span).Slice(this.Count, count).Fill((UInt64)value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldType));
            }
        }

        protected void Fill(Memory<byte> field, Int16 value, int count)
        {
            MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(this.Count, count).Fill(value);
        }

        protected void Fill(Memory<byte> field, Int32 value, int count)
        {
            MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(this.Count, count).Fill(value);
        }

        protected void Fill(Memory<byte> field, UInt32 value, int count)
        {
            MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(this.Count, count).Fill(value);
        }
    }
}
