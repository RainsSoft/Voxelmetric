using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code.Common.Events;
using Voxelmetric.Code.Core.StateManager;

namespace Voxelmetric.Code.Core.Serialization
{
    public class SaveProgress : IEventListener<ChunkStateExternal>
    {
        private readonly List<Chunk> chunksToSave = new List<Chunk>();
        public readonly int totalChunksToSave = 0;
        private int m_Progress = 0;

        public SaveProgress(List<Chunk> chunks)
        {
            if (chunks == null)
                return;

            if (chunks.Count <= 0)
            {
                m_Progress = 100;
                return;
            }

            chunksToSave.AddRange(chunks);
            totalChunksToSave = chunksToSave.Count;

            // Register at each chunk
            for (int i = 0; i < chunksToSave.Count; i++)
            {
                Chunk chunk = chunksToSave[i];
                ChunkStateManagerClient stateManager = chunk.StateManager;
                stateManager.Subscribe(this, ChunkStateExternal.Saved, true);
            }
        }

        public int GetProgress()
        {
            return m_Progress;
        }

        private void SaveCompleteForChunk(Chunk chunk)
        {
            chunksToSave.Remove(chunk);
            m_Progress = Mathf.FloorToInt((totalChunksToSave - chunksToSave.Count) / (float)totalChunksToSave * 100);
        }

        void IEventListener<ChunkStateExternal>.OnNotified(IEventSource<ChunkStateExternal> source, ChunkStateExternal evt)
        {
            // Unsubscribe from any further events
            ChunkStateManagerClient stateManager = (ChunkStateManagerClient)source;
            stateManager.Subscribe(this, evt, false);

            Assert.IsTrue(evt == ChunkStateExternal.Saved);
            if (evt == ChunkStateExternal.Saved)
            {
                if (!chunksToSave.Contains(stateManager.Chunk))
                    return;
                SaveCompleteForChunk(stateManager.Chunk);
            }
        }
    }
}
