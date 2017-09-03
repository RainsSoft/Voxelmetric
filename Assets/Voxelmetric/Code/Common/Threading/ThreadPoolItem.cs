using System;

namespace Voxelmetric.Code.Common.Threading
{
    public interface IAThreadPoolItem : ITaskPoolItem
    {
        int ThreadID { get; }
        long Time { get; }
    }

    public class ThreadPoolItem<T> : IAThreadPoolItem
    {
        private Action<T> action;
        private T arg;

        public int ThreadID { get; private set; }

        public long Time { get; private set; }

        public ThreadPoolItem() { }

        public ThreadPoolItem(ThreadPool pool, Action<T> action, T arg, long time = long.MaxValue)
        {
            this.action = action;
            this.arg = arg;
            ThreadID = pool.GenerateThreadID();
            Time = time;
        }

        public ThreadPoolItem(int threadID, Action<T> action, T arg, long time = long.MaxValue)
        {
            this.action = action;
            this.arg = arg;
            ThreadID = threadID;
            Time = time;
        }

        public void Set(ThreadPool pool, Action<T> action, T arg, long time = long.MaxValue)
        {
            this.action = action;
            this.arg = arg;
            ThreadID = pool.GenerateThreadID();
            Time = time;
        }

        public void Set(int threadID, Action<T> action, T arg, long time = long.MaxValue)
        {
            this.action = action;
            this.arg = arg;
            ThreadID = threadID;
            Time = time;
        }

        public void Run()
        {
            action(arg);
        }
    }
}
