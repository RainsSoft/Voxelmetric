using UnityEngine;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core.Operations
{
    /// <summary>
    /// Base class for range-based setBlock operations. Overload OnSetBlocks to create your own modify operation.
    /// </summary>
    public abstract class ModifyOpRange : ModifyOp
    {
        protected Vector3Int m_Min;
        protected Vector3Int m_Max;

        protected ModifyOpRange(BlockData blockData, Vector3Int min, Vector3Int max, bool setBlockModified,
            ModifyBlockContext parentContext = null) : base(blockData, setBlockModified, parentContext)
        {
            this.m_Min = min;
            this.m_Max = max;
        }

        protected override bool IsRanged()
        {
            return m_Min != m_Max;
        }

        protected override void OnPostSetBlocks(ChunkBlocks blocks)
        {
            if (parentContext != null)
                parentContext.ChildActionFinished();

            if (IsRanged())
            {
                ChunkBlocks neighborBlocks = null;

                if (blocks.NeedToHandleNeighbors(ref m_Min))
                {
                    // Left side
                    if (blocks.NeedToHandleNeighbors(ref m_Min))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Min, Direction.west);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(Env.ChunkSize, m_Min.y, m_Min.z);
                            Vector3Int to = new Vector3Int(Env.ChunkSize, m_Max.y, m_Max.z);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                    // Bottom side
                    if (blocks.NeedToHandleNeighbors(ref m_Min))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Min, Direction.down);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(m_Min.x, Env.ChunkSize, m_Min.z);
                            Vector3Int to = new Vector3Int(m_Max.x, Env.ChunkSize, m_Max.z);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                    // Back side
                    if (blocks.NeedToHandleNeighbors(ref m_Min))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Min, Direction.south);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(m_Min.x, m_Min.y, Env.ChunkSize);
                            Vector3Int to = new Vector3Int(m_Max.x, m_Max.y, Env.ChunkSize);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                }

                if (blocks.NeedToHandleNeighbors(ref m_Max))
                {
                    // Right side
                    if (blocks.NeedToHandleNeighbors(ref m_Max))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Max, Direction.east);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(-1, m_Min.y, m_Min.z);
                            Vector3Int to = new Vector3Int(-1, m_Max.y, m_Max.z);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                    // Upper side
                    if (blocks.NeedToHandleNeighbors(ref m_Max))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Max, Direction.up);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(m_Min.x, -1, m_Min.z);
                            Vector3Int to = new Vector3Int(m_Max.x, -1, m_Max.z);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                    // Front side
                    if (blocks.NeedToHandleNeighbors(ref m_Max))
                    {
                        neighborBlocks = blocks.HandleNeighbor(ref m_Max, Direction.north);
                        if (neighborBlocks != null)
                        {
                            Vector3Int from = new Vector3Int(m_Min.x, m_Min.y, -1);
                            Vector3Int to = new Vector3Int(m_Max.x, m_Max.y, -1);
                            OnSetBlocksRaw(neighborBlocks, ref from, ref to);
                        }
                    }
                }
            }
            else
            {
                blocks.HandleNeighbors(blockData, m_Min);
            }
        }
    }
}
