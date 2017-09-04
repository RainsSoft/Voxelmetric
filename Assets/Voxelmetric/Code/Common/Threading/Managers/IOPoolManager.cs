using System.Collections.Generic;

namespace Voxelmetric.Code.Common.Threading.Managers
{
    public static class IOPoolManager
    {
        private static readonly List<ITaskPoolItem> workItems = new List<ITaskPoolItem>(2048);

        public static void Add(ITaskPoolItem action)
        {
            workItems.Add(action);
        }

        public static void Commit()
        {
            if (workItems.Count <= 0)
                return;

            // Commit all the work we have
            if (Features.USE_THREADED_ID)
            {
                TaskPool pool = Globals.IOPool;

                for (int i = 0; i < workItems.Count; i++)
                {
                    pool.AddItem(workItems[i]);
                }
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
            return Globals.IOPool.ToString();
        }
    }
}
