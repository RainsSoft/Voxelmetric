using System;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;

namespace Voxelmetric.Code.Common.Memory
{
    public class MarshalMemPool
    {
        //! Allocated memory in bytes
        private readonly int size;
        //! Position to the beggining of the buffer
        private readonly long buffer;
        //! Current position in allocate array (m_buffer+x)
        private long m_Pos;

        public MarshalMemPool(int initialSize)
        {
            size = initialSize;
            // Allocate all memory we can
            buffer = (long)Marshal.AllocHGlobal(initialSize);
            m_Pos = buffer;
        }

        ~MarshalMemPool()
        {
            // Release all allocated memory in the end
            Marshal.FreeHGlobal((IntPtr)buffer);
        }

        public IntPtr Pop(int size)
        {
            // Do not take more than we can give!
            Assert.IsTrue(m_Pos + size < buffer + this.size);

            m_Pos += size;
            return (IntPtr)m_Pos;
        }

        public void Push(int size)
        {
            // Do not return than we gave!
            Assert.IsTrue(m_Pos >= buffer);

            m_Pos -= size;
        }

        public int Left
        {
            get { return size - (int)(m_Pos - buffer); }
        }

        public override string ToString()
        {
            return string.Format("{0}/{1}", (int)(m_Pos - buffer), size);
        }
    }
}
