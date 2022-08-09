using Apache.Arrow;
using Apache.Arrow.Types;
using System;
using Array = System.Array;

namespace iLand.Extensions
{
    public class ArrowArrayExtensions
    {
        private static ArrayData WrapInArrayData(IArrowType dataType, Memory<byte> memory, int length)
        {
            return new ArrayData(dataType, length, 0, 0, new ArrowBuffer[] { ArrowBuffer.Empty, new ArrowBuffer(memory) }, Array.Empty<ArrayData>());
        }

        public static FloatArray WrapInFloat(Memory<byte> memory)
        {
            return new FloatArray(ArrowArrayExtensions.WrapInArrayData(FloatType.Default, memory, memory.Length / sizeof(float)));
        }

        public static Int32Array WrapInInt32(Memory<byte> memory)
        {
            return new Int32Array(ArrowArrayExtensions.WrapInArrayData(Int32Type.Default, memory, memory.Length / sizeof(Int32)));
        }
    }
}
