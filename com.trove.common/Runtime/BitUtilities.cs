using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Trove
{
    public static class BitUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit(int inValue, int bitPosition)
        {
            return ((inValue >> bitPosition) & 1) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(bool one, ref int inValue, int bitPosition)
        {
            if (one)
            {
                inValue |= (1 << bitPosition);
            }
            else
            {
                inValue = inValue & ~(1 << bitPosition);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit(uint inValue, int bitPosition)
        {
            return ((inValue >> bitPosition) & 1) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(bool one, ref uint inValue, int bitPosition)
        {
            if (one)
            {
                inValue |= (uint)(1 << bitPosition);
            }
            else
            {
                inValue = (uint)(inValue & ~(1 << bitPosition));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit(byte inValue, int bitPosition)
        {
            return ((inValue >> bitPosition) & 1) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(bool one, ref byte inValue, int bitPosition)
        {
            if (one)
            {
                inValue = (byte)(inValue | (1 << bitPosition));
            }
            else
            {
                inValue = (byte)(inValue & ~(1 << bitPosition));
            }
        }
    }
}