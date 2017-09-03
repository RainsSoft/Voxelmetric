using System.Collections.Generic;
using Voxelmetric.Code.Utilities;

namespace Voxelmetric.Code.Common.Threading.Managers
{
    public static class WorkPoolManager
    {
        private static readonly List<IAThreadPoolItem> workItems = new List<IAThreadPoolItem>(2048);

        private static readonly TimeBudgetHandler timeBudget = Features.UseThreadPool ? null : new TimeBudgetHandler(10);

        public static void Add(IAThreadPoolItem action)
        {
            workItems.Add(action);
        }

        public static void Commit()
        {
            if (workItems.Count <= 0)
                return;

            // Commit all the work we have
            if (Features.UseThreadPool)
            {
                ThreadPool pool = Globals.WorkPool;

                // Sort our work items by threadID
                workItems.Sort(
                    (x, y) =>
                    {
                        int ret = x.ThreadID.CompareTo(y.ThreadID);
                        if (ret == 0)
                            ret = x.Time.CompareTo(y.Time);
                        return ret;
                    });

                // Commit items to their respective task thread.
                // Instead of commiting tasks one by one, we take them all and commit
                // them at once
                TaskPool tp;
                int from = 0, to = 0;
                for (int i = 0; i < workItems.Count - 1; i++)
                {
                    IAThreadPoolItem curr = workItems[i];
                    IAThreadPoolItem next = workItems[i + 1];
                    if (curr.ThreadID == next.ThreadID)
                    {
                        to = i + 1;
                        continue;
                    }

                    tp = pool.GetTaskPool(curr.ThreadID);
                    tp.Lock();
                    for (int j = from; j <= to; j++)
                    {
                        tp.AddItemUnsafe(workItems[j]);
                    }
                    tp.Unlock();

                    from = i + 1;
                    to = from;
                }

                tp = pool.GetTaskPool(workItems[from].ThreadID);
                tp.Lock();
                for (int j = from; j <= to; j++)
                {
                    tp.AddItemUnsafe(workItems[j]);
                }
                tp.Unlock();


            }
            //else
            //{
            //    for (int i = 0; i < workItems.Count; i++)
            //    {
            //        timeBudget.StartMeasurement();
            //        workItems[i].Run();
            //        timeBudget.StopMeasurement();

            //        // If the tasks take too much time to finish, spread them out over multiple
            //        // frames to avoid performance spikes
            //        if (!timeBudget.HasTimeBudget)
            //        {
            //            workItems.RemoveRange(0, i + 1);
            //            return;
            //        }
            //    }
            //}

            // Remove processed work items
            workItems.Clear();
        }

        public new static string ToString()
        {
            return Features.UseThreadPool ? Globals.WorkPool.ToString() : workItems.Count.ToString();
        }
    }
}
