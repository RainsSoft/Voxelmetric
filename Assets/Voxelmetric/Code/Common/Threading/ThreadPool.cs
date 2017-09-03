using System;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.MemoryPooling;

namespace Voxelmetric.Code.Common.Threading
{
    public class ThreadPool
    {
        private bool m_Started;
        private volatile int m_NextThreadIndex = 0;

        //! Threads used by thread pool
        private readonly TaskPool[] pools;

        //! Diagnostics
        private readonly StringBuilder stringBuilder = new StringBuilder(128);

        public ThreadPool()
        {
            m_Started = false;

            // If the number of threads is not correctly specified, create as many as possible minus one (taking
            // all available core is not effective - there's still the main thread we should not forget).
            // Allways create at least one thread, however.
            int threadCnt = Features.UseThreadPool ? Mathf.Max(Environment.ProcessorCount - 1, 1) : 1;
            pools = Helpers.CreateArray1D<TaskPool>(threadCnt);
            // NOTE: Normally, I would simply call CreateAndInitArray1D, however, any attempt to allocate memory
            // for TaskPool in this contructor ends up with Unity3D crashing :(
        }

        public int GenerateThreadID()
        {
            m_NextThreadIndex = GetThreadIDFromIndex(m_NextThreadIndex + 1);
            return m_NextThreadIndex;
        }

        public int GetThreadIDFromIndex(int index)
        {
            return Helpers.Mod(index, pools.Length);
        }

        public LocalPools GetPool(int index)
        {
            int id = GetThreadIDFromIndex(index);
            return pools[id].Pools;
        }

        public TaskPool GetTaskPool(int index)
        {
            return pools[index];
        }

        public void Start()
        {
            if (m_Started)
                return;
            m_Started = true;

            for (int i = 0; i < pools.Length; i++)
            {
                pools[i] = new TaskPool();
                pools[i].Start();
            }
        }

        public void AddItem(ITaskPoolItem item)
        {
            int threadID = GenerateThreadID();
            pools[threadID].AddItem(item);
        }

        public void AddItem<T>(Action<T> action) where T : class
        {
            int threadID = GenerateThreadID();
            pools[threadID].AddItem(action);
        }

        public void AddItem<T>(int threadID, Action<T> action) where T : class
        {
            // Assume a proper index is passed as an arugment
            Assert.IsTrue(threadID >= 0 && threadID < pools.Length);
            pools[threadID].AddItem(action);
        }

        public void AddItem<T>(Action<T> action, T arg)
        {
            int threadID = GenerateThreadID();
            pools[threadID].AddItem(action, arg);
        }

        public void AddItem<T>(int threadID, Action<T> action, T arg)
        {
            // Assume a proper index is passed as an arugment
            Assert.IsTrue(threadID >= 0 && threadID < pools.Length);
            pools[threadID].AddItem(action, arg);
        }

        public int PooledItemCnt
        {
            get
            {
                int items = 0;
                for (int i = 0; i < pools.Length; i++)
                    items += pools[i].Size;
                return items;
            }
        }

        public int Size
        {
            get { return pools.Length; }
        }

        public override string ToString()
        {
            stringBuilder.Length = 0;
            for (int i = 0; i < pools.Length - 1; i++)
                stringBuilder.ConcatFormat("{0}, ", pools[i].ToString());
            return stringBuilder.ConcatFormat("{0}", pools[pools.Length - 1].ToString()).ToString();
        }
    }
}
