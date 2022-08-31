using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace iLand.Extensions
{
    internal static class DebugV
    {
        [Conditional("DEBUG")]
        public static void Assert(Vector128<float> condition)
        {
            Debug.Assert(Avx.MoveMask(condition) == Simd128.MaskAllTrue);
        }

        [Conditional("DEBUG")]
        public static void Assert(Vector128<int> condition)
        {
            DebugV.Assert(condition.AsSingle());
        }

        [Conditional("DEBUG")]
        public static void Assert(Vector256<float> condition)
        {
            Debug.Assert(Avx.MoveMask(condition) == Simd256.MaskAllTrue);
        }

        [Conditional("DEBUG")]
        public static void Assert(Vector256<int> condition)
        {
            DebugV.Assert(condition.AsSingle());
        }
    }
}
