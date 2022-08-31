using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace iLand.Extensions
{
    public static class Avx2Extensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> BroadcastScalarToVector128(float value)
        {
            Vector128<float> value128 = Vector128.CreateScalarUnsafe(value);
            return Avx2.BroadcastScalarToVector128(value128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> BroadcastScalarToVector128(int value)
        {
            Vector128<int> value128 = Vector128.CreateScalarUnsafe(value);
            return Avx2.BroadcastScalarToVector128(value128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> BroadcastScalarToVector256(float value)
        {
            Vector128<float> value128 = Vector128.CreateScalarUnsafe(value);
            return Avx2.BroadcastScalarToVector256(value128);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> BroadcastScalarToVector256(int value)
        {
            Vector128<int> value128 = Vector128.CreateScalarUnsafe(value);
            return Avx2.BroadcastScalarToVector256(value128);
        }
    }
}