using System.Collections.Generic;

namespace Voxelmetric.Code.Common.Memory
{
    public sealed class ArrayPool<T> : IArrayPool<T>
    {
        //! Stack of arrays
        private readonly Stack<T[]> arrays;
        //! Length of array to allocate
        private readonly int arrayLength;

        public ArrayPool(int length, int initialCapacity, int initialSize)
        {
            arrayLength = length;

            if (initialSize > 0)
            {
                // Init
                arrays = new Stack<T[]>(initialSize < initialCapacity ? initialCapacity : initialSize);

                for (int i = 0; i < initialSize; ++i)
                {
                    var item = Helpers.CreateArray1D<T>(length);
                    arrays.Push(item);
                }
            }
            else
            {
                // Init
                arrays = new Stack<T[]>(initialCapacity);
            }
        }

        /// <summary>
        ///     Retrieves an array from the top of the pool
        /// </summary>
        public T[] Pop()
        {
            return arrays.Count == 0 ? new T[arrayLength] : arrays.Pop();
        }

        /// <summary>
        ///     Returns an array back to the pool
        /// </summary>
        public void Push(T[] item)
        {
            if (item == null)
                return;

            arrays.Push(item);
        }

        public override string ToString()
        {
            return arrays.Count.ToString();
        }
    }
}
