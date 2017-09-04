using System.Collections.Generic;
using UnityEngine;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core
{
    public sealed class ChunkLogic
    {
        private readonly Chunk chunk;
        private float m_RandomUpdateTime;
        private readonly List<BlockAndTimer> scheduledUpdates = new List<BlockAndTimer>();

        public ChunkLogic(Chunk chunk)
        {
            this.chunk = chunk;
            Reset();
        }

        public void Reset()
        {
            m_RandomUpdateTime = 0;
            scheduledUpdates.Clear();
        }

        public void Update()
        {
            m_RandomUpdateTime += Time.deltaTime;
            if (m_RandomUpdateTime >= chunk.World.Config.randomUpdateFrequency)
            {
                m_RandomUpdateTime = 0;

                Vector3Int randomVector3Int = new Vector3Int(
                    Voxelmetric.resources.random.Next(0, Env.CHUNK_SIZE),
                    Voxelmetric.resources.random.Next(0, Env.CHUNK_SIZE),
                    Voxelmetric.resources.random.Next(0, Env.CHUNK_SIZE)
                    );

                chunk.Blocks.GetBlock(ref randomVector3Int).RandomUpdate(chunk, ref randomVector3Int);

                // Process Scheduled Updates
                for (int i = 0; i < scheduledUpdates.Count; i++)
                {
                    scheduledUpdates[i] = new BlockAndTimer(
                        scheduledUpdates[i].pos,
                        scheduledUpdates[i].time - chunk.World.Config.randomUpdateFrequency
                        );

                    if (scheduledUpdates[i].time <= 0)
                    {
                        Vector3Int pos = scheduledUpdates[i].pos;
                        Block block = chunk.Blocks.GetBlock(ref pos);
                        block.ScheduledUpdate(chunk, ref pos);
                        scheduledUpdates.RemoveAt(i);
                        i--;
                    }
                }
            }

        }

        public void AddScheduledUpdate(Vector3Int vector3Int, float time)
        {
            scheduledUpdates.Add(new BlockAndTimer(vector3Int, time));
        }
    }
}
