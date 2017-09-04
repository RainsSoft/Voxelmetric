using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Core.Serialization;

namespace Voxelmetric.Code.Core.StateManager
{
    public abstract class ChunkStateManager : ChunkEvent
    {
        public Chunk Chunk { get; private set; }

        //! Save handler for chunk
        protected readonly Save save;

        //! Specifies whether there's a task running on this Chunk
        protected volatile bool m_TaskRunning;
        //! Next state after currently finished state
        protected ChunkState m_NextState;
        //! States waiting to be processed
        protected ChunkState m_PendingStates;
        //! Tasks already executed
        protected ChunkState m_CompletedStates;
        //! Just like m_completedStates, but it is synchronized on the main thread once a check for m_taskRunning is passed
        protected ChunkState m_CompletedStatesSafe;

        //! If true, removal of chunk has been requested and no further requests are going to be accepted
        protected bool m_RemovalRequested;
        protected bool m_IsSaveNeeded;

        protected ChunkStateManager(Chunk chunk)
        {
            this.Chunk = chunk;
            if (Features.USE_SERIALIZATION)
                save = new Save(chunk);
        }

        public virtual void Init()
        {
            // Request this chunk to be generated
            OnNotified(this, ChunkState.LoadData);
        }

        public virtual void Reset()
        {
            Clear();

            m_NextState = m_NextState.Reset();
            m_PendingStates = m_PendingStates.Reset();
            m_CompletedStates = m_CompletedStates.Reset();
            m_CompletedStatesSafe = m_CompletedStates;
            m_RemovalRequested = false;
            m_IsSaveNeeded = false;

            m_TaskRunning = false;

            if (save != null)
                save.Reset();
        }

        public bool CanUpdate()
        {
            // Do not do any processing as long as there is any task still running
            // Note that this check is not thread-safe because this value can be changed from a different thread. However,
            // we do not care. The worst thing that can happen is that we read a value which is one frame old. So be it.
            // Thanks to being this relaxed approach we do not need any synchronization primitives at all.
            if (m_TaskRunning)
                return false;

            // Synchronize the value with what we have on a different thread. It would be no big deal not having this at
            // all. However, it is technically more correct.
            m_CompletedStatesSafe = m_CompletedStates;

            // Once this Chunk is marked as finished we ignore any further requests and won't perform any updates
            return !m_CompletedStatesSafe.Check(ChunkState.Remove);
        }

        public abstract void Update();

        public void RequestState(ChunkState state)
        {
            switch (state)
            {
                case ChunkState.PrepareSaveData:
                    {
                        m_IsSaveNeeded = true;
                    }
                    break;

                case ChunkState.Remove:
                    {
                        if (m_RemovalRequested)
                            return;
                        m_RemovalRequested = true;

                        if (Features.SERIALIZE_CHUNK_WHEN_UNLOADING)
                            OnNotified(this, ChunkState.PrepareSaveData);
                        OnNotified(this, ChunkState.Remove);
                    }
                    break;
            }

            m_PendingStates = m_PendingStates.Set(state);
        }

        public void ResetRequest(ChunkState state)
        {
            m_PendingStates = m_PendingStates.Reset(state);
        }

        public bool IsStateCompleted(ChunkState state)
        {
            return m_CompletedStatesSafe.Check(state);
        }


        public bool IsSavePossible
        {
            get { return save != null && !m_RemovalRequested && m_CompletedStatesSafe.Check(ChunkState.Generate); }
        }

        public bool IsUpdateBlocksPossible
        {
            get { return !m_PendingStates.Check(ChunkState.PrepareSaveData) && !m_PendingStates.Check(ChunkState.SaveData); }
        }

        public abstract void SetMeshBuilt();
        public abstract void SetColliderBuilt();
    }
}
