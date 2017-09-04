using System;
using System.Collections.Generic;
using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Core.Operations;
using Voxelmetric.Code.Core.StateManager;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code
{
    public static class Voxelmetric
    {
        //Used as a manager class with references to classes treated like singletons
        public static readonly VoxelmetricResources resources = new VoxelmetricResources();

        public static void SetBlock(World world, ref Vector3Int pos, BlockData blockData, Action<ModifyBlockContext> onAction = null)
        {
            world.Blocks.Modify(ref pos, blockData, true, onAction);
        }

        public static void SetBlockRange(World world, ref Vector3Int posFrom, ref Vector3Int posTo, BlockData blockData, Action<ModifyBlockContext> onAction = null)
        {
            world.Blocks.ModifyRange(ref posFrom, ref posTo, blockData, true, onAction);
        }

        public static Block GetBlock(World world, ref Vector3Int pos)
        {
            return world.Blocks.GetBlock(ref pos);
        }

        /// <summary>
        /// Sends a save request to all chunk currently loaded
        /// </summary>
        /// <param name="world">World holding chunks</param>
        /// <returns>List of chunks waiting to be saved.</returns>
        public static List<Chunk> SaveAll(World world)
        {
            if (world == null || !Features.USE_SERIALIZATION)
                return null;

            List<Chunk> chunksToSave = new List<Chunk>();

            foreach (Chunk chunk in world.Chunks.ChunkCollection)
            {
                // Ignore chunks that can't be saved at the moment
                ChunkStateManagerClient stateManager = chunk.StateManager;
                if (!stateManager.IsSavePossible)
                    continue;

                chunksToSave.Add(chunk);
                stateManager.RequestState(ChunkState.PrepareSaveData);
            }

            return chunksToSave;
        }
    }
}
