using Apache.Arrow;
using Apache.Arrow.Types;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace iLand.Output.Memory
{
    internal class ArrowMemory
    {
        protected const int DefaultMaximumRecordsPerBatch = 10 * 1000 * 1000;

        public int BatchSize { get; private init; }
        public int Capacity { get; private init; }
        public int Count { get; protected set; }

        public IList<RecordBatch> RecordBatches { get; private init; }

        protected ArrowMemory(int capacityInRecords, int batchSize)
        {
            if (capacityInRecords < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityInRecords), "Capacity of " + capacityInRecords + " is zero or negative.");
            }
            if ((batchSize < 10 * 1000) || (batchSize > 100 * 1000 * 1000))
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Record batch size of " + batchSize + " is unexpectedly large or small.");
            }

            this.BatchSize = batchSize;
            this.Capacity = capacityInRecords;
            this.Count = 0;

            int batches = capacityInRecords / batchSize + (capacityInRecords % batchSize != 0 ? 1 : 0);
            this.RecordBatches = new List<RecordBatch>(batches);
        }

        // provide CopyFirstN() overloads specialized to source type
        // While CopyFirstN<TSource>() is cleaner at the member function level it's more complex for callers as often TSource
        // cannot be inferred.
        protected static void CopyFirstN(ReadOnlySpan<float> source, Memory<byte> field, int start, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, float>(field.Span).Slice(start, count));
        }

        protected static void CopyFirstN(ReadOnlySpan<Int16> source, Memory<byte> field, int start, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(start, count));
        }

        protected static void CopyFirstN(ReadOnlySpan<Int32> source, Memory<byte> field, int start, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(start, count));
        }

        protected static void CopyFirstN(ReadOnlySpan<UInt16> source, Memory<byte> field, int start, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, UInt16>(field.Span).Slice(start, count));
        }

        protected static void CopyFirstN(ReadOnlySpan<UInt32> source, Memory<byte> field, int start, int count)
        {
            source[..count].CopyTo(MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(start, count));
        }

        protected static void CopyN(ReadOnlySpan<float> source, int sourceStart, Memory<byte> field, int destinationStart, int count)
        {
            int sourceEnd = sourceStart + count;
            source[sourceStart..sourceEnd].CopyTo(MemoryMarshal.Cast<byte, float>(field.Span).Slice(destinationStart, count));
        }

        protected static void CopyN(ReadOnlySpan<Int16> source, int sourceStart, Memory<byte> field, int destinationStart, int count)
        {
            int sourceEnd = sourceStart + count;
            source[sourceStart..sourceEnd].CopyTo(MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(destinationStart, count));
        }

        protected static void CopyN(ReadOnlySpan<UInt16> source, int sourceStart, Memory<byte> field, int destinationStart, int count)
        {
            int sourceEnd = sourceStart + count;
            source[sourceStart..sourceEnd].CopyTo(MemoryMarshal.Cast<byte, UInt16>(field.Span).Slice(destinationStart, count));
        }

        protected static void CopyN(ReadOnlySpan<UInt32> source, int sourceStart, Memory<byte> field, int destinationStart, int count)
        {
            int sourceEnd = sourceStart + count;
            source[sourceStart..sourceEnd].CopyTo(MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(destinationStart, count));
        }

        protected static void Fill(Memory<byte> field, IntegerType fieldType, Int32 value, int start, int count)
        {
            switch (fieldType.BitWidth)
            {
                case 8:
                    MemoryMarshal.Cast<byte, sbyte>(field.Span).Slice(start, count).Fill((sbyte)value);
                    break;
                case 16:
                    MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(start, count).Fill((Int16)value);
                    break;
                case 32:
                    MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(start, count).Fill(value);
                    break;
                case 64:
                    MemoryMarshal.Cast<byte, Int64>(field.Span).Slice(start, count).Fill((Int64)value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldType));
            }
        }

        protected static void Fill(Memory<byte> field, IntegerType fieldType, UInt32 value, int start, int count)
        {
            switch (fieldType.BitWidth)
            {
                case 8:
                    MemoryMarshal.Cast<byte, byte>(field.Span).Slice(start, count).Fill((byte)value);
                    break;
                case 16:
                    MemoryMarshal.Cast<byte, UInt16>(field.Span).Slice(start, count).Fill((UInt16)value);
                    break;
                case 32:
                    MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(start, count).Fill((UInt32)value);
                    break;
                case 64:
                    MemoryMarshal.Cast<byte, UInt64>(field.Span).Slice(start, count).Fill((UInt64)value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldType));
            }
        }

        protected static void Fill(Memory<byte> field, Int16 value, int start, int count)
        {
            MemoryMarshal.Cast<byte, Int16>(field.Span).Slice(start, count).Fill(value);
        }

        protected static void Fill(Memory<byte> field, Int32 value, int start, int count)
        {
            MemoryMarshal.Cast<byte, Int32>(field.Span).Slice(start, count).Fill(value);
        }

        protected static void Fill(Memory<byte> field, UInt32 value, int start, int count)
        {
            MemoryMarshal.Cast<byte, UInt32>(field.Span).Slice(start, count).Fill(value);
        }

        protected (int startIndexInCurrentBatch, int recordsToCopyToCurrentBatch) GetBatchIndicesForAdd(int recordsToAdd)
        {
            int startIndexInCurrentBatch = this.Count % this.BatchSize;
            int capacityRemainingInRecordBatch = this.BatchSize - startIndexInCurrentBatch;
            int recordsToCopyToCurrentBatch = Int32.Min(recordsToAdd, capacityRemainingInRecordBatch);
            return (startIndexInCurrentBatch, recordsToCopyToCurrentBatch);
        }

        protected int GetNextBatchLength()
        {
            int remainingCapacity = this.Capacity - this.Count;
            return Int32.Min(remainingCapacity, this.BatchSize);
        }
    }
}
