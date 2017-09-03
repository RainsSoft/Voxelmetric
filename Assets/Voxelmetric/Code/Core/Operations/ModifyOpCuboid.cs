using UnityEngine;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core.Operations
{
    public sealed class ModifyOpCuboid: ModifyOpRange
    {
        /// <summary>
        /// Performs a ranged set operation of cuboid shape
        /// </summary>
        /// <param name="blockData">BlockData to place at the given location</param>
        /// <param name="min">Starting positon in local chunk coordinates</param>
        /// <param name="max">Ending position in local chunk coordinates</param>
        /// <param name="setBlockModified">Set to true to mark chunk data as modified</param>
        /// <param name="parentContext">Context of a parent which performed this operation</param>
        public ModifyOpCuboid(BlockData blockData, Vector3Int min, Vector3Int max, bool setBlockModified,
            ModifyBlockContext parentContext = null): base(blockData, min, max, setBlockModified, parentContext)
        {
        }
        
        protected override void OnSetBlocks(ChunkBlocks blocks)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(m_Min.x, m_Min.y, m_Min.z);
            int yOffset = Env.ChunkSizeWithPaddingPow2-(m_Max.z-m_Min.z+1) * Env.ChunkSizeWithPadding;
            int zOffset = Env.ChunkSizeWithPadding-(m_Max.x-m_Min.x+1);

            for (int y = m_Min.y; y<=m_Max.y; ++y, index+=yOffset)
            {
                for (int z = m_Min.z; z<=m_Max.z; ++z, index+=zOffset)
                {
                    for (int x = m_Min.x; x<=m_Max.x; ++x, ++index)
                    {
                        blocks.ProcessSetBlock(blockData, index, setBlockModified);
                    }
                }
            }
        }

        protected override void OnSetBlocksRaw(ChunkBlocks blocks, ref Vector3Int from, ref Vector3Int to)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(from.x, from.y, from.z);
            int yOffset = Env.ChunkSizeWithPaddingPow2-(to.z-from.z+1) * Env.ChunkSizeWithPadding;
            int zOffset = Env.ChunkSizeWithPadding-(to.x-from.x+1);

            for (int y = from.y; y <= to.y; ++y, index+=yOffset)
            {
                for (int z = from.z; z <= to.z; ++z, index+=zOffset)
                {
                    for (int x = from.x; x <= to.x; ++x, ++index)
                    {
                        blocks.SetRaw(index, blockData);
                    }
                }
            }
        }
    }
}
