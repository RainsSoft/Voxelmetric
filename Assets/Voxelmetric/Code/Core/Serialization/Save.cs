using System.Collections.Generic;
using System.IO;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.IO;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core.Serialization
{
    public class Save : IBinarizable
    {
        public static readonly short SaveVersion = 1;

        //! If true and a differential serialization is enabled, it says that there was once a difference
        //! Without this headers would not be serialized if a change was made on chunk that would return it to its default state
        private bool m_HadDifferentialChange = false;

        public Chunk Chunk { get; private set; }
        public bool IsDifferential { get; private set; }

        //! A list of modified positions
        private BlockPos[] m_PositionsModified;
        //! A list of modified blocks
        private BlockData[] m_BlocksModified;

        // Temporary structures
        private byte[] m_PositionsBytes;
        private byte[] m_BlocksBytes;


        public Save(Chunk chunk)
        {
            Chunk = chunk;
            IsDifferential = false;
        }

        public void Reset()
        {
            m_HadDifferentialChange = false;
            MarkAsProcessed();

            // Reset temporary buffers
            m_PositionsBytes = null;
            m_BlocksBytes = null;

            // Release the memory allocated by temporary buffers
            m_PositionsModified = null;
            m_BlocksModified = null;
        }

        public void MarkAsProcessed()
        {
            // Release the memory allocated by temporary buffers
            m_PositionsModified = null;
            m_BlocksModified = null;
        }

        public bool IsBinarizeNecessary()
        {
            if (m_BlocksModified == null &&
                !Features.UseDifferentialSerialization_ForceSaveHeaders &&
                !m_HadDifferentialChange)
                return false;

            return true;
        }

        public bool Binarize(BinaryWriter bw)
        {
            // Do not serialize if there's no chunk data and empty chunk serialization is turned off
            if (m_BlocksModified == null)
            {
                if (!Features.UseDifferentialSerialization_ForceSaveHeaders &&
                    !m_HadDifferentialChange
                    )
                    return false;
                m_HadDifferentialChange = false;
            }

            bw.Write(SaveVersion);
            bw.Write((byte)(Features.UseDifferentialSerialization ? 1 : 0));
            bw.Write(Env.ChunkSizePow3);
            bw.Write(Chunk.Blocks.NonEmptyBlocks);

            int blockPosSize = StructSerialization.TSSize<BlockPos>.ValueSize;
            int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;

            // Chunk data
            if (Features.UseDifferentialSerialization)
            {
                if (m_BlocksModified == null)
                    bw.Write(0);
                else
                {
                    int posLenBytes = m_BlocksModified.Length * blockPosSize;
                    int blkLenBytes = m_BlocksModified.Length * blockDataSize;

                    bw.Write(m_BlocksModified.Length);
                    bw.Write(m_PositionsBytes, 0, posLenBytes);
                    bw.Write(m_BlocksBytes, 0, blkLenBytes);
                }
            }
            //else
            //{
            //    int blkLenBytes = m_BlocksModified.Length * blockDataSize;

            //    // Write compressed data to file
            //    bw.Write(blkLenBytes);
            //    bw.Write(m_BlocksBytes, 0, blkLenBytes);
            //}

            // We no longer need the temporary buffers
            m_PositionsBytes = null;
            m_BlocksBytes = null;

            return true;
        }

        public bool Debinarize(BinaryReader br)
        {
            bool success = true;

            // Read the version number
            int version = br.ReadInt16();
            if (version != SaveVersion)
                return false;

            // 0/1 allowed for IsDifferential
            byte isDifferential = br.ReadByte();
            if (isDifferential != 0 && isDifferential != 1)
            {
                success = false;
                goto deserializeFail;
            }
            IsDifferential = isDifferential == 1;

            // Current chunk size must match the saved chunk size
            int chunkBlocks = br.ReadInt32();
            if (chunkBlocks != Env.ChunkSizePow3)
            {
                success = false;
                goto deserializeFail;
            }

            // NonEmptyBlocks must be a sane number in chunkBlocks range
            int nonEmptyBlocks = br.ReadInt32();
            if (nonEmptyBlocks < 0 || nonEmptyBlocks > chunkBlocks)
            {
                success = false;
                goto deserializeFail;
            }
            Chunk.Blocks.NonEmptyBlocks = nonEmptyBlocks;

            while (true)
            {
                if (IsDifferential)
                {
                    int lenBlocks = br.ReadInt32();
                    if (lenBlocks > 0)
                    {
                        int blockPosSize = StructSerialization.TSSize<BlockPos>.ValueSize;
                        int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;

                        int posLenBytes = lenBlocks * blockPosSize;
                        int blkLenBytes = lenBlocks * blockDataSize;

                        m_PositionsBytes = new byte[posLenBytes];
                        int read = br.Read(m_PositionsBytes, 0, posLenBytes);
                        if (read != posLenBytes)
                        {
                            // Length must match
                            success = false;
                            goto deserializeFail;
                        }

                        m_BlocksBytes = new byte[blkLenBytes];
                        read = br.Read(m_BlocksBytes, 0, blkLenBytes);
                        if (read != blkLenBytes)
                        {
                            // Length must match
                            success = false;
                            goto deserializeFail;
                        }
                    }
                    else
                    {
                        m_BlocksBytes = null;
                        m_PositionsBytes = null;
                    }
                }
                else
                {
                    // If somebody switched from full to differential serialization, make it so that the next time the chunk is serialized it's saved as diff
                    if (Features.UseDifferentialSerialization)
                        m_HadDifferentialChange = true;

                    int blkLenBytes = br.ReadInt32();
                    m_BlocksBytes = new byte[blkLenBytes];

                    // Read raw data
                    int readLength = br.Read(m_BlocksBytes, 0, blkLenBytes);
                    if (readLength != blkLenBytes)
                    {
                        // Length must match
                        success = false;
                        goto deserializeFail;
                    }
                }

                break;
            }
            deserializeFail:
            if (!success)
            {
                // Revert any changes we performed on our chunk
                Chunk.Blocks.NonEmptyBlocks = -1;

                m_PositionsBytes = null;
                m_BlocksBytes = null;
            }

            return success;
        }

        public bool DoCompression()
        {
            if (Features.UseDifferentialSerialization)
            {
                if (m_BlocksModified != null)
                {
                    var provider = Chunk.World.BlockProvider;
                    int blockPosSize = StructSerialization.TSSize<BlockPos>.ValueSize;
                    int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;

                    int posLenBytes = m_BlocksModified.Length * blockPosSize;
                    int blkLenBytes = m_BlocksModified.Length * blockDataSize;
                    m_PositionsBytes = new byte[posLenBytes];
                    m_BlocksBytes = new byte[blkLenBytes];

                    unsafe
                    {
                        // Pack positions to a byte array
                        fixed (byte* pDst = m_PositionsBytes)
                        {
                            for (int i = 0, j = 0; i < m_BlocksModified.Length; i++, j += blockPosSize)
                            {
                                *(BlockPos*)&pDst[j] = m_PositionsModified[i];
                            }
                        }
                        // Pack block data to a byte array
                        fixed (BlockData* pBD = m_BlocksModified)
                        fixed (byte* pDst = m_BlocksBytes)
                        {
                            for (int i = 0, j = 0; i < m_BlocksModified.Length; i++, j += blockDataSize)
                            {
                                BlockData* bd = &pBD[i];
                                // Convert block types from internal optimized version into global types
                                ushort typeInConfig = provider.GetConfig(bd->Type).TypeInConfig;

                                *(BlockData*)&pDst[j] = new BlockData(typeInConfig, bd->Solid);
                            }
                        }
                    }
                }
                else
                {
                    m_PositionsBytes = null;
                    m_BlocksBytes = null;
                }
            }
            //else
            //{
            //    LocalPools pools = Chunk.pools;
            //    var provider = Chunk.world.blockProvider;

            //    int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;
            //    int requestedByteSize = Env.ChunkSizePow3 * blockDataSize;

            //    // Pop large enough buffers from the pool
            //    byte[] tmp = pools.byteArrayPool.Pop(requestedByteSize);
            //    byte[] bytesCompressed = pools.byteArrayPool.Pop(requestedByteSize);
            //    {
            //        ChunkBlocks blocks = Chunk.blocks;
            //        int i = 0;

            //        int index = Helpers.ZeroChunkIndex;
            //        int yOffset = Env.ChunkSizeWithPaddingPow2 - Env.ChunkSize * Env.ChunkSizeWithPadding;
            //        int zOffset = Env.ChunkSizeWithPadding - Env.ChunkSize;

            //        for (int y = 0; y < Env.ChunkSize; ++y, index += yOffset)
            //        {
            //            for (int z = 0; z < Env.ChunkSize; ++z, index += zOffset)
            //            {
            //                for (int x = 0; x < Env.ChunkSize; ++x, i += blockDataSize, ++index)
            //                {
            //                    // Convert block types from internal optimized version into global types
            //                    BlockData bd = blocks.Get(index + x);
            //                    ushort typeInConfig = provider.GetConfig(bd.Type).TypeInConfig;

            //                    // Write updated block data to destination buffer
            //                    unsafe
            //                    {
            //                        fixed (byte* pDst = tmp)
            //                        {
            //                            *(BlockData*)&pDst[i] = new BlockData(typeInConfig, bd.Solid);
            //                        }
            //                    }
            //                }
            //            }
            //        }

            //        // Compress bytes
            //        int blkLenBytes = CLZF2.lzf_compress(tmp, requestedByteSize, ref bytesCompressed);
            //        m_BlocksBytes = new byte[blkLenBytes];

            //        // Copy data from a temporary buffer to block buffer
            //        Array.Copy(bytesCompressed, 0, m_BlocksBytes, 0, blkLenBytes);
            //    }
            //    // Return our temporary buffer back to the pool
            //    pools.byteArrayPool.Push(bytesCompressed);
            //    pools.byteArrayPool.Push(tmp);
            //}

            return true;
        }

        public bool DoDecompression()
        {
            LocalPools pools = Chunk.Pools;
            var provider = Chunk.World.BlockProvider;

            if (IsDifferential)
            {
                if (m_PositionsBytes != null && m_BlocksBytes != null)
                {
                    int blockPosSize = StructSerialization.TSSize<BlockPos>.ValueSize;
                    int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;

                    m_PositionsModified = new BlockPos[m_PositionsBytes.Length / blockPosSize];
                    m_BlocksModified = new BlockData[m_BlocksBytes.Length / blockDataSize];

                    int i, j;
                    unsafe
                    {
                        // Extract positions
                        fixed (byte* pSrc = m_PositionsBytes)
                        {
                            for (i = 0, j = 0; j < m_PositionsModified.Length; i += blockPosSize, j++)
                            {
                                m_PositionsModified[j] = *(BlockPos*)&pSrc[i];
                            }
                        }
                        // Extract block data
                        fixed (byte* pSrc = m_BlocksBytes)
                        {
                            for (i = 0, j = 0; j < m_BlocksModified.Length; i += blockDataSize, j++)
                            {
                                BlockData* bd = (BlockData*)&pSrc[i];
                                // Convert global block types into internal optimized version
                                ushort type = provider.GetTypeFromTypeInConfig(bd->Type);

                                m_BlocksModified[j] = new BlockData(type, bd->Solid);
                            }
                        }
                    }
                }
            }
            else
            {
                m_PositionsBytes = null;
                if (m_BlocksBytes != null)
                {
                    int blockDataSize = StructSerialization.TSSize<BlockData>.ValueSize;
                    int requestedByteSize = Env.ChunkSizePow3 * blockDataSize;

                    // Pop a large enough buffers from the pool
                    var bytes = pools.byteArrayPool.Pop(requestedByteSize);
                    {
                        // Decompress data
                        int decompressedLength = CLZF2.lzf_decompress(m_BlocksBytes, m_BlocksBytes.Length, ref bytes);
                        if (decompressedLength != Env.ChunkSizePow3 * blockDataSize)
                        {
                            m_BlocksBytes = null;
                            return false;
                        }

                        // Fill chunk with decompressed data
                        ChunkBlocks blocks = Chunk.Blocks;
                        int i = 0;
                        unsafe
                        {
                            fixed (byte* pSrc = bytes)
                            {
                                int index = Helpers.ZeroChunkIndex;
                                int yOffset = Env.ChunkSizeWithPaddingPow2 - Env.ChunkSize * Env.ChunkSizeWithPadding;
                                int zOffset = Env.ChunkSizeWithPadding - Env.ChunkSize;

                                for (int y = 0; y < Env.ChunkSize; ++y, index += yOffset)
                                {
                                    for (int z = 0; z < Env.ChunkSize; ++z, index += zOffset)
                                    {
                                        for (int x = 0; x < Env.ChunkSize; ++x, i += blockDataSize, ++index)
                                        {
                                            BlockData* bd = (BlockData*)&pSrc[i];

                                            // Convert global block type into internal optimized version
                                            ushort type = provider.GetTypeFromTypeInConfig(bd->Type);

                                            blocks.SetRaw(index + x, new BlockData(type, bd->Solid));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    // Return our temporary buffer back to the pool
                    pools.byteArrayPool.Push(bytes);
                }
            }

            // We no longer need the temporary buffers
            m_PositionsBytes = null;
            m_BlocksBytes = null;

            return true;
        }

        public void ConsumeChanges()
        {
            //if (!Features.UseDifferentialSerialization)
            //    return;

            ChunkBlocks blocks = Chunk.Blocks;
            if (blocks.modifiedBlocks.Count <= 0)
                return;

            m_HadDifferentialChange = true;

            Dictionary<BlockPos, BlockData> blocksDictionary = new Dictionary<BlockPos, BlockData>();

            // Create a map of modified blocks and their positions
            // TODO: Depending on the amount of changes this could become a performance bottleneck
            for (int i = 0; i < blocks.modifiedBlocks.Count; i++)
            {
                var pos = blocks.modifiedBlocks[i];
                // Remove any existing blocks in the dictionary. They come from the existing save and are overwritten
                blocksDictionary.Remove(pos);
                blocksDictionary.Add(pos, blocks.Get(Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z)));
            }

            int cnt = blocksDictionary.Keys.Count;
            m_BlocksModified = new BlockData[cnt];
            m_PositionsModified = new BlockPos[cnt];

            int index = 0;
            foreach (var pair in blocksDictionary)
            {
                m_BlocksModified[index] = pair.Value;
                m_PositionsModified[index] = pair.Key;
                ++index;
            }
        }

        public void CommitChanges()
        {
            if (!IsDifferential)
                return;

            // Rewrite generated blocks with differential positions
            if (m_BlocksModified != null)
            {
                for (int i = 0; i < m_BlocksModified.Length; i++)
                {
                    BlockPos pos = m_PositionsModified[i];
                    Chunk.Blocks.SetRaw(Helpers.GetChunkIndex1DFrom3D(pos.x, pos.y, pos.z), m_BlocksModified[i]);
                }
            }

            MarkAsProcessed();
        }
    }
}
