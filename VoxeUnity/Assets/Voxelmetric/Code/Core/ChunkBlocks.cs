﻿using System;
using System.Collections.Generic;
using System.Text;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Core.StateManager;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources.Blocks;
using Voxelmetric.Code.Utilities;
using Voxelmetric.Code.VM;

namespace Voxelmetric.Code.Core
{
    public sealed class ChunkBlocks
    {
        public Chunk chunk { get; private set; }

        private Block[] m_blockTypes;

        //! Array of block data
        private BlockData[] blocks = Helpers.CreateArray1D<BlockData>(Env.ChunkSizeWithPaddingPow3);


        //! Number of blocks which are not air (non-empty blocks)
        public int NonEmptyBlocks = 0;

        //! Queue of setBlock operations to execute
        private readonly List<SetBlockContext> m_setBlockQueue = new List<SetBlockContext>();

        private byte[] receiveBuffer;
        private int receiveIndex;

        private long lastUpdateTimeGeometry;
        private long lastUpdateTimeCollider;
        private int rebuildMaskGeometry;
        private int rebuildMaskCollider;

        public readonly List<BlockPos> modifiedBlocks = new List<BlockPos>();
        public bool contentsInvalidated;
        public bool topLayerInvalidated;
        public bool bottomLayerInvalidated;

        private static byte[] emptyBytes;
        public static byte[] EmptyBytes
        {
            get
            {
                if (emptyBytes==null)
                    emptyBytes = new byte[16384]; // TODO: Validate whether this is fine
                return emptyBytes;
            }
        }

        public ChunkBlocks(Chunk chunk)
        {
            this.chunk = chunk;
        }

        public void Init()
        {
            m_blockTypes = chunk.world.blockProvider.BlockTypes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("contentsInvliadated=");
            sb.Append(contentsInvalidated.ToString());
            return sb.ToString();
        }

        public void Copy(ChunkBlocks src, int srcIndex, int dstIndex, int length)
        {
            Array.Copy(src.blocks, srcIndex, blocks, dstIndex, length);
        }

        public void Reset()
        {
            NonEmptyBlocks = 0;
            // Reset internal parts of the chunk buffer. Edges not touched
            Array.Clear(blocks, 0, Env.ChunkSizeWithPaddingPow3);

            lastUpdateTimeGeometry = 0;
            lastUpdateTimeCollider = 0;
            rebuildMaskGeometry = -1;
            rebuildMaskCollider = -1;

            contentsInvalidated = true;
            topLayerInvalidated = true;
            bottomLayerInvalidated = true;

            modifiedBlocks.Clear();
        }

        public void Update()
        {
            ChunkStateManagerClient stateManager = (ChunkStateManagerClient)chunk.stateManager;

            if (m_setBlockQueue.Count>0)
            {
                if (rebuildMaskGeometry<0)
                    rebuildMaskGeometry = 0;
                if (rebuildMaskCollider<0)
                    rebuildMaskCollider = 0;

                // Modify blocks
                for (int j = 0; j<m_setBlockQueue.Count; j++)
                {
                    SetBlockContext context = m_setBlockQueue[j];

                    int x, y, z;
                    Helpers.GetChunkIndex3DFrom1D(context.Index, out x, out y, out z);

                    Vector3Int pos = new Vector3Int(x, y, z);
                    Vector3Int globalPos = pos+chunk.pos;

                    BlockData oldBlockData = blocks[context.Index];

                    Block oldBlock = m_blockTypes[oldBlockData.Type];
                    Block newBlock = m_blockTypes[context.Block.Type];
                    oldBlock.OnDestroy(chunk, pos);
                    newBlock.OnCreate(chunk, pos);

                    SetInternal(context.Index, ref pos, context.Block);

                    if (context.SetBlockModified)
                    {
                        BlockModified(new BlockPos(x, y, z), globalPos, context.Block);

                        chunk.blocks.contentsInvalidated = true;
                    }

                    if (
                        // Only check neighbors if it is still needed
                        rebuildMaskGeometry==0x3f ||
                        // Only check neighbors when it is a change of a block on a chunk's edge
                        (((pos.x+1)&Env.ChunkMask)>1 &&
                         ((pos.y+1)&Env.ChunkMask)>1 &&
                         ((pos.z+1)&Env.ChunkMask)>1)
                        )
                        continue;

                    int cx = chunk.pos.x;
                    int cy = chunk.pos.y;
                    int cz = chunk.pos.z;

                    // If it is an edge position, notify neighbor as well
                    // Iterate over neighbors and decide which ones should be notified to rebuild
                    for (int i = 0; i<stateManager.Listeners.Length; i++)
                    {
                        ChunkEvent listener = stateManager.Listeners[i];
                        if (listener==null)
                            continue;

                        // No further checks needed once we know all neighbors need to be notified
                        if (rebuildMaskGeometry==0x3f)
                            break;

                        ChunkStateManagerClient listenerClient = (ChunkStateManagerClient)listener;
                        Chunk listenerChunk = listenerClient.chunk;

                        int lx = listenerChunk.pos.x;
                        int ly = listenerChunk.pos.y;
                        int lz = listenerChunk.pos.z;

                        if ((ly == cy || lz == cz))
                        {
                            // Section to the left
                            if ((pos.x == 0) && (lx + Env.ChunkSize == cx))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);
                                
                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(Env.ChunkSize, y, z);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                            // Section to the right
                            else if((pos.x == Env.ChunkMask) && (lx - Env.ChunkSize == cx))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);

                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(-1, y, z);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                        }

                        if ((lx == cx || lz == cz))
                        {
                            // Section to the bottom
                            if ((pos.y == 0) && (ly + Env.ChunkSize == cy))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);

                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(x, Env.ChunkSize, y);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                            // Section to the top
                            else if ((pos.y == Env.ChunkMask) && (ly - Env.ChunkSize == cy))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);

                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(x, -1, y);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                        }

                        if ((ly == cy || lx == cx))
                        {
                            // Section to the back
                            if ((pos.z == 0) && (lz + Env.ChunkSize == cz))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);

                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(x, y, Env.ChunkSize);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                            // Section to the front
                            else if ((pos.z == Env.ChunkMask) && (lz - Env.ChunkSize == cz))
                            {
                                rebuildMaskGeometry = rebuildMaskGeometry | (1 << i);

                                // Mirror the block to the neighbor edge
                                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(x, y, -1);
                                listenerChunk.blocks.blocks[neighborIndex] = context.Block;
                            }
                        }
                    }
                }

                rebuildMaskCollider |= rebuildMaskGeometry;

                m_setBlockQueue.Clear();
            }

            long now = Globals.Watch.ElapsedMilliseconds;

            // Request a geometry update at most 10 times a second
            if (rebuildMaskGeometry>=0 && now-lastUpdateTimeGeometry>=100)
            {
                lastUpdateTimeGeometry = now;

                // Request rebuild on this chunk
                stateManager.RequestState(ChunkState.BuildVerticesNow);;

                // Notify neighbors that they need to rebuilt their geometry
                if (rebuildMaskGeometry>0)
                {
                    for (int j = 0; j<stateManager.Listeners.Length; j++)
                    {
                        ChunkStateManagerClient listener = (ChunkStateManagerClient)stateManager.Listeners[j];
                        if (listener!=null && ((rebuildMaskGeometry>>j)&1)!=0)
                        {
                            // Request rebuild on neighbor chunks
                            listener.RequestState(ChunkState.BuildVerticesNow);
                        }
                    }
                }

                rebuildMaskGeometry = -1;
            }

            // Request a collider update at most 4 times a second
            if (chunk.NeedsCollider && rebuildMaskCollider>=0 && now-lastUpdateTimeCollider>=250)
            {
                lastUpdateTimeCollider = now;

                // Request rebuild on this chunk
                stateManager.RequestState(ChunkState.BuildCollider);

                // Notify neighbors that they need to rebuilt their geometry
                if (rebuildMaskCollider > 0)
                {
                    for (int j = 0; j < stateManager.Listeners.Length; j++)
                    {
                        ChunkStateManagerClient listener = (ChunkStateManagerClient)stateManager.Listeners[j];
                        if (listener != null && ((rebuildMaskCollider >> j) & 1) != 0)
                        {
                            // Request rebuild on neighbor chunks
                            if (listener.chunk.NeedsCollider)
                                listener.RequestState(ChunkState.BuildCollider);
                        }
                    }
                }

                rebuildMaskCollider = -1;
            }
        }

        /// <summary>
        /// Returns block data from a position within the chunk
        /// </summary>
        /// <param name="pos">Position in local chunk coordinates</param>
        /// <returns>The block at the position</returns>
        public BlockData Get(Vector3Int pos)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z);
            return blocks[index];
        }

        /// <summary>
        /// Returns a block from a position within the chunk
        /// </summary>
        /// <param name="pos">Position in local chunk coordinates</param>
        /// <returns>The block at the position</returns>
        public Block GetBlock(Vector3Int pos)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z);
            return m_blockTypes[blocks[index].Type];
        }

        private void SetInternal(int index, ref Vector3Int pos, BlockData blockData)
        {
            // Nothing for us to do if there was no change
            BlockData oldBlockData = blocks[index];
            if (oldBlockData.Type == blockData.Type)
                return;

            // Update non-empty block count if we're inside non-padded area
            if (pos.x>=0 && pos.x<Env.ChunkSize &&
                pos.y>=0 && pos.y<Env.ChunkSize &&
                pos.z>=0 && pos.z<Env.ChunkSize)
            {
                if (blockData.Type==BlockProvider.AirType)
                    --NonEmptyBlocks;
                else
                    ++NonEmptyBlocks;
            }

            blocks[index] = blockData;
        }

        /// <summary>
        /// Sets the block at the given position
        /// </summary>
        /// <param name="pos">Position in local chunk coordinates</param>
        /// <param name="blockData">A block to be placed on a given position</param>
        public void Set(Vector3Int pos, BlockData blockData)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z);
            SetInternal(index, ref pos, blockData);
        }

        /// <summary>
        /// Sets the block at the given position
        /// </summary>
        /// <param name="pos">Position in local chunk coordinates</param>
        /// <param name="blockData">BlockData to place at the given location</param>
        /// <param name="setBlockModified">Set to true to mark chunk data as modified</param>
        public void Modify(Vector3Int pos, BlockData blockData, bool setBlockModified)
        {
            int index = Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z);

            // Nothing for us to do if the block did not change
            BlockData oldBlockData = blocks[index];
            if (oldBlockData.Type==blockData.Type)
                return;

            m_setBlockQueue.Add(new SetBlockContext(index, blockData, setBlockModified));
        }

        public void BlockModified(BlockPos blockPos, Vector3Int globalPos, BlockData blockData)
        {
            // If this is the server log the changed block so that it can be saved
            if (chunk.world.networking.isServer)
            {
                if (chunk.world.networking.allowConnections)
                    chunk.world.networking.server.BroadcastChange(globalPos, blockData, -1);

                if (!modifiedBlocks.Contains(blockPos))
                    modifiedBlocks.Add(blockPos);
            }
            else // if this is not the server send the change to the server to sync
            {
                chunk.world.networking.client.BroadcastChange(globalPos, blockData);
            }
        }

        private void InitializeChunkDataReceive(int index, int size)
        {
            receiveIndex = index;
            receiveBuffer = new byte[size];
        }

        public void ReceiveChunkData(byte[] buffer)
        {
            int index = BitConverter.ToInt32(buffer, VmServer.headerSize);
            int size = BitConverter.ToInt32(buffer, VmServer.headerSize+4);

            if (receiveBuffer==null)
                InitializeChunkDataReceive(index, size);

            TranscribeChunkData(buffer, VmServer.leaderSize);
        }

        private void TranscribeChunkData(byte[] buffer, int offset)
        {
            for (int o = offset; o<buffer.Length; o++)
            {
                receiveBuffer[receiveIndex] = buffer[o];
                receiveIndex++;

                if (receiveIndex==receiveBuffer.Length)
                {
                    FinishChunkDataReceive();
                    return;
                }
            }
        }

        private void FinishChunkDataReceive()
        {
            if(!InitFromBytes())
                Reset();

            ChunkStateManagerClient stateManager = (ChunkStateManagerClient)chunk.stateManager;
            ChunkStateManagerClient.OnGenerateDataOverNetworkDone(stateManager);

            receiveBuffer = null;
            receiveIndex = 0;
        }

        #region "Temporary network compression solution"

        public byte[] ToBytes()
        {
            List<byte> buffer = new List<byte>();
            buffer.AddRange(BitConverter.GetBytes(NonEmptyBlocks));
            if (NonEmptyBlocks > 0)
            {
                int sameBlockCount = 1;
                BlockData lastBlockData = blocks[0];

                for (int index = 1; index < Env.ChunkSizeWithPaddingPow3; index++)
                {
                    BlockData blockData = blocks[index];
                    if (blockData.Equals(lastBlockData))
                    {
                        // If this is the same as the last block added increase the count
                        ++sameBlockCount;
                    }
                    else
                    {
                        buffer.AddRange(BitConverter.GetBytes(sameBlockCount));
                        buffer.AddRange(BlockData.ToByteArray(lastBlockData));

                        sameBlockCount = 1;
                        lastBlockData = blockData;
                    }
                }

                buffer.AddRange(BitConverter.GetBytes(sameBlockCount));
                buffer.AddRange(BlockData.ToByteArray(lastBlockData));
            }

            return buffer.ToArray();
        }

        private bool InitFromBytes()
        {
            if (receiveBuffer == null || receiveBuffer.Length < 4)
                return false;

            NonEmptyBlocks = BitConverter.ToInt32(receiveBuffer, 0);
            if (NonEmptyBlocks > 0)
            {
                int dataOffset = 4;
                int blockOffset = 0;

                while (dataOffset < receiveBuffer.Length)
                {
                    int sameBlockCount = BitConverter.ToInt32(receiveBuffer, dataOffset + 4); // 4 bytes
                    BlockData bd = new BlockData(BlockData.RestoreBlockData(receiveBuffer, dataOffset + 8)); // 2 bytes
                    for (int i = blockOffset; i < blockOffset + sameBlockCount; i++)
                        blocks[i] = bd;

                    dataOffset += 4 + 2;
                    blockOffset += sameBlockCount;
                }
            }

            return true;
        }

        #endregion
    }
}