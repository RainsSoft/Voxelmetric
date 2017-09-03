using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.MemoryPooling;

namespace Voxelmetric.Code.Common.Threading
{
    public sealed class TaskPool : IDisposable
    {
        //! Each thread contains an object pool
        public LocalPools Pools { get; private set; }

        private List<ITaskPoolItem> m_Items; // list of tasks
        private readonly object lockObject = new object();

        private readonly AutoResetEvent resetEvent; // event for notifing worker thread about work
        private readonly Thread thread; // worker thread

        private bool m_stop;

        //! Diagnostics
        private int m_Current, m_Max;
        private readonly StringBuilder stringBuilder = new StringBuilder(32);

        public TaskPool()
        {
            Pools = new LocalPools();

            m_Items = new List<ITaskPoolItem>();
            resetEvent = new AutoResetEvent(false);
            thread = new Thread(ThreadFunc)
            {
                IsBackground = true
            };
        }

        ~TaskPool()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            Stop();

            if (disposing)
            {
                // dispose managed resources
                resetEvent.Close();
            }
            // free native resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            thread.Start();
        }

        public void Stop()
        {
            m_stop = true;
            resetEvent.Set();
        }

        public int Size
        {
            get { return m_Items.Count; }
        }

        public void AddItem(ITaskPoolItem item)
        {
            // Do not add new action in we re stopped or action is invalid
            if (item == null || m_stop)
                return;

            // Add task to task list and notify the worker thread
            lock (lockObject)
            {
                m_Items.Add(item);
            }
            resetEvent.Set();
        }

        public void AddItem<T>(Action<T> action) where T : class
        {
            // Do not add new action in we re stopped or action is invalid
            if (action == null || m_stop)
                return;

            // Add task to task list and notify the worker thread
            lock (lockObject)
            {
                m_Items.Add(new TaskPoolItem<T>(action, null));
            }
            resetEvent.Set();
        }

        public void AddItem<T>(Action<T> action, T arg)
        {
            // Do not add new action in we re stopped or action is invalid
            if (action == null || m_stop)
                return;

            // Add task to task list and notify the worker thread
            lock (lockObject)
            {
                m_Items.Add(new TaskPoolItem<T>(action, arg));
            }
            resetEvent.Set();
        }

        public void AddItemUnsafe([NotNull] ITaskPoolItem item)
        {
            m_Items.Add(item);
        }

        public void AddItemUnsafe<T>([NotNull] Action<T> action) where T : class
        {
            // Add task to task list
            m_Items.Add(new TaskPoolItem<T>(action, null));
        }

        public void AddItemUnsafe<T>([NotNull] Action<T> action, T arg)
        {
            // Add task to task list
            m_Items.Add(new TaskPoolItem<T>(action, arg));
        }

        public void Lock()
        {
            Monitor.Enter(lockObject);
        }

        public void Unlock()
        {
            Monitor.Exit(lockObject);
            resetEvent.Set();
        }

        private void ThreadFunc()
        {
            var actions = new List<ITaskPoolItem>();

            while (!m_stop)
            {
                // Swap action list pointers
                lock (lockObject)
                {
                    var tmp = m_Items;
                    m_Items = actions;
                    actions = tmp;
                }

                m_Max = actions.Count;

                // Execute all tasks in a row
                for (m_Current = 0; m_Current < actions.Count; m_Current++)
                {
                    // Execute the action
                    // Note, it's up to action to provide exception handling
                    ITaskPoolItem poolItem = actions[m_Current];

#if DEBUG
                    try
                    {
#endif
                        poolItem.Run();
#if DEBUG
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                        throw;
                    }
#endif
                }
                actions.Clear();
                m_Current = m_Max = 0;

                // Wait for next tasks
                resetEvent.WaitOne();
            }
        }

        public override string ToString()
        {
            stringBuilder.Length = 0;
            return stringBuilder.ConcatFormat("{0}/{1}", m_Current, m_Max).ToString();
        }
    }
}
