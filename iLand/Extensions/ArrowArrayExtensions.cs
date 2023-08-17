using Apache.Arrow;
using Apache.Arrow.Types;
using System;
using Array = System.Array;

namespace iLand.Extensions
{
    public class ArrowArrayExtensions
    {
        // Arrow 12.0 does not support replacement dictionaries from C#, preventing string table implementation
        // As of 9.0, it appears the current state of support is the necessary C# classes exist but the dictionary batch required to
        // accompany the record batch is silently not written in feather files (https://arrow.apache.org/docs/status.html#ipc-format,
        // https://arrow.apache.org/docs/format/Columnar.html). The result is that, while writes from C# appear successful, reads in R
        // fail with Key error: Dictionary with id 1 not found.
        // See also https://github.com/apache/arrow/blob/master/csharp/src/Apache.Arrow/Ipc/ArrowStreamWriter.cs WriteDictionary(Field)
        // public static readonly DictionaryType StringTable256Type = new(Int32Type.Default, StringType.Default, false);

        //public static DictionaryArray MakeDictionaryColumn(Memory<byte> indicies, IList<string> values)
        //{
        //    StringArray.Builder valueArray = new();
        //    for (int valueIndex = 0; valueIndex < values.Count; ++valueIndex)
        //    {
        //        valueArray.Append(values[valueIndex]);
        //    }

        //    Int32Array indexArray = new(ArrowArrayExtensions.WrapInArrayData(UInt8Type.Default, indicies, indicies.Length));
        //    return new DictionaryArray(new(UInt8Type.Default, StringType.Default, false), indexArray, valueArray.Build());
        //}

        public static IArrowArray Wrap(IntegerType integerDataType, Memory<byte> memory)
        {
            if (integerDataType.IsSigned)
            {
                return integerDataType.BitWidth switch
                {
                    8 => ArrowArrayExtensions.WrapInInt8(memory),
                    16 => ArrowArrayExtensions.WrapInInt16(memory),
                    32 => ArrowArrayExtensions.WrapInInt32(memory),
                    64 => ArrowArrayExtensions.WrapInInt64(memory),
                    _ => throw new ArgumentOutOfRangeException(nameof(integerDataType))
                };
            }
            else
            {
                return integerDataType.BitWidth switch
                {
                    8 => ArrowArrayExtensions.WrapInUInt8(memory),
                    16 => ArrowArrayExtensions.WrapInUInt16(memory),
                    32 => ArrowArrayExtensions.WrapInUInt32(memory),
                    64 => ArrowArrayExtensions.WrapInUInt64(memory),
                    _ => throw new ArgumentOutOfRangeException(nameof(integerDataType))
                };
            }
        }

        private static ArrayData WrapInArrayData(IArrowType dataType, Memory<byte> memory, int length)
        {
            return new ArrayData(dataType, length, 0, 0, new ArrowBuffer[] { ArrowBuffer.Empty, new ArrowBuffer(memory) }, Array.Empty<ArrayData>());
        }

        public static FloatArray WrapInFloat(Memory<byte> memory)
        {
            return new FloatArray(ArrowArrayExtensions.WrapInArrayData(FloatType.Default, memory, memory.Length / sizeof(float)));
        }

        public static Int8Array WrapInInt8(Memory<byte> memory)
        {
            return new Int8Array(ArrowArrayExtensions.WrapInArrayData(Int8Type.Default, memory, memory.Length));
        }

        public static Int16Array WrapInInt16(Memory<byte> memory)
        {
            return new Int16Array(ArrowArrayExtensions.WrapInArrayData(Int16Type.Default, memory, memory.Length / sizeof(Int16)));
        }

        public static Int32Array WrapInInt32(Memory<byte> memory)
        {
            return new Int32Array(ArrowArrayExtensions.WrapInArrayData(Int32Type.Default, memory, memory.Length / sizeof(Int32)));
        }

        public static Int64Array WrapInInt64(Memory<byte> memory)
        {
            return new Int64Array(ArrowArrayExtensions.WrapInArrayData(Int64Type.Default, memory, memory.Length / sizeof(Int64)));
        }

        public static UInt8Array WrapInUInt8(Memory<byte> memory)
        {
            return new UInt8Array(ArrowArrayExtensions.WrapInArrayData(UInt8Type.Default, memory, memory.Length));
        }

        public static UInt16Array WrapInUInt16(Memory<byte> memory)
        {
            return new UInt16Array(ArrowArrayExtensions.WrapInArrayData(UInt16Type.Default, memory, memory.Length / sizeof(UInt16)));
        }

        public static UInt32Array WrapInUInt32(Memory<byte> memory)
        {
            return new UInt32Array(ArrowArrayExtensions.WrapInArrayData(UInt32Type.Default, memory, memory.Length / sizeof(UInt32)));
        }

        public static UInt64Array WrapInUInt64(Memory<byte> memory)
        {
            return new UInt64Array(ArrowArrayExtensions.WrapInArrayData(UInt64Type.Default, memory, memory.Length / sizeof(UInt64)));
        }
    }
}
