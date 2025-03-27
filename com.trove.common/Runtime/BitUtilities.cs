using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.EventSystems;

namespace Trove
{
    public unsafe interface IBitMask
    {
        public byte* GetPtr();
    }
    
    public unsafe struct BitMaskIterator
    {
        private readonly byte* _bytePtr;
        private readonly int _bitLength;
        private int _currentBitPosition;

        public BitMaskIterator(byte* bytePtr, int bitLength)
        {
            _bytePtr = bytePtr;
            _bitLength = bitLength;
            _currentBitPosition = 0;
        }

        /// <summary>
        /// Return false when finished iterating the bitmask
        /// </summary>
        public bool GetNextEnabledBit(out int bitPosition)
        {
            int currentPositionAsInt = *(_bytePtr + (long)_currentBitPosition);
            _currentBitPosition += math.lzcnt(currentPositionAsInt);
            if (_currentBitPosition < _bitLength)
            {
                bitPosition = _currentBitPosition;
                return true;
            }

            bitPosition = -1;
            return false;
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask8 : IBitMask
    {
        [FieldOffset(0)]
        public byte B0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref B0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 8);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask16 : IBitMask
    {
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref B0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 16);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask32 : IBitMask
    {
        [FieldOffset(0)]
        public byte B0;
        [FieldOffset(1)]
        public byte B1;
        [FieldOffset(2)]
        public byte B2;
        [FieldOffset(3)]
        public byte B3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref B0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 32);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask64 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask32 BM0;
        [FieldOffset(4)]
        public BitMask32 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 64);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask128 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask64 BM0;
        [FieldOffset(8)]
        public BitMask64 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 128);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask256 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask128 BM0;
        [FieldOffset(16)]
        public BitMask128 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 256);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask512 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask256 BM0;
        [FieldOffset(32)]
        public BitMask256 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 512);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask1024 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask512 BM0;
        [FieldOffset(64)]
        public BitMask512 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 1024);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask2048 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask1024 BM0;
        [FieldOffset(128)]
        public BitMask1024 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 2048);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask4096 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask2048 BM0;
        [FieldOffset(256)]
        public BitMask2048 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 4096);
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct BitMask8192 : IBitMask
    {
        [FieldOffset(0)]
        public BitMask4096 BM0;
        [FieldOffset(512)]
        public BitMask4096 BM1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* GetPtr()
        {
            return (byte*)UnsafeUtility.AddressOf(ref BM0);
        }

        public unsafe BitMaskIterator GetMaskIterator()
        {
            return new BitMaskIterator(GetPtr(), 8192);
        }
    }
    
    public unsafe static class BitUtilities
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit(byte* inPtr, int bitPosition)
        {
            return ((inPtr[bitPosition] >> bitPosition) & 1) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit(bool one, byte* inPtr, int bitPosition)
        {
            if (one)
            {
                inPtr[bitPosition] = (byte)(inPtr[bitPosition] | (1 << bitPosition));
            }
            else
            {
                inPtr[bitPosition] = (byte)(inPtr[bitPosition] & ~(1 << bitPosition));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetBit<T>(in T inBitMask, int bitPosition) where T : unmanaged, IBitMask
        {
            return GetBit(inBitMask.GetPtr(), bitPosition);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetBit<T>(bool one, ref T inBitMask, int bitPosition) where T : unmanaged, IBitMask
        {
            SetBit(one, inBitMask.GetPtr(), bitPosition);
        }
    }
}