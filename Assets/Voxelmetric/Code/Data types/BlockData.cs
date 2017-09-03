using System;
using System.Runtime.InteropServices;

namespace Voxelmetric.Code.Data_types
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BlockData : IEquatable<BlockData>
    {
        public static readonly ushort TypeMask = 0x7FFF;

        /* Bits
         * 15 - solid
         * 14 - 0 - block type
        */
        private readonly ushort data;

        public BlockData(ushort data)
        {
            this.data = data;
        }

        public BlockData(ushort type, bool solid)
        {
            data = (ushort)(type & 0x7FFF);
            if (solid)
                data |= 0x8000;
        }

        public ushort Data { get { return data; } }

        /// <summary>
        /// Fast lookup of whether the block is solid without having to take a look into block arrays
        /// </summary>
        public bool Solid { get { return (data >> 15) != 0; } }

        /// <summary>
        /// Information about block's type
        /// </summary>
        public ushort Type { get { return (ushort)(data & TypeMask); } }

        public static ushort RestoreBlockData(byte[] data, int offset)
        {
            return BitConverter.ToUInt16(data, offset);
        }

        public static byte[] ToByteArray(BlockData data)
        {
            return BitConverter.GetBytes(data.data);
        }

        #region Object comparison

        public bool Equals(BlockData other)
        {
            return data == other.data;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is BlockData && Equals((BlockData)obj);
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }

        public static bool operator ==(BlockData data1, BlockData data2)
        {
            return data1.data == data2.data;
        }

        public static bool operator !=(BlockData data1, BlockData data2)
        {
            return data1.data != data2.data;
        }

        #endregion
    }
}
