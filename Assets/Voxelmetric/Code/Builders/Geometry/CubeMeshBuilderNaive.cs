using System;
using UnityEngine;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources.Blocks;

namespace Voxelmetric.Code.Builders.Geometry
{
    /// <summary>
    /// Generates a cubical mesh with merged faces
    /// </summary>
    public class CubeMeshBuilderNaive : MergedFacesMeshBuilder
    {
        public CubeMeshBuilderNaive(float scale, int sideSize) : base(scale, sideSize) { }

        protected override bool CanConsiderBlock(Block block)
        {
            // Skip air data
            return block.Type != BlockProvider.AIR_TYPE;
        }

        protected override bool CanCreateBox(Block block, Block neighbor)
        {
            return block.Type == neighbor.Type;
        }

        protected override void BuildBox(Chunk chunk, Block block, int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        {
            // All faces in the are build in the following order:
            //     1--2
            //     |  |
            //     |  |
            //     0--3

            int sizeWithPadding = m_SideSize + Env.CHUNK_PADDING_2;
            int sizeWithPaddingPow2 = sizeWithPadding * sizeWithPadding;

            var blocks = chunk.Blocks;
            var pools = chunk.Pools;
            var listeners = chunk.StateManager.Listeners;

            // Custom blocks have their own rules
            if (block.Custom)
            {
                for (int yy = minY; yy < maxY; yy++)
                {
                    for (int zz = minZ; zz < maxZ; zz++)
                    {
                        for (int xx = minX; xx < maxX; xx++)
                        {
                            Vector3Int pos = new Vector3Int(xx, yy, zz);
                            block.BuildBlock(chunk, ref pos, block.RenderMaterialID);
                        }
                    }
                }

                return;
            }

            int n, w, h, l, k, maskIndex;
            Vector3Int texturePos = new Vector3Int(minX, minY, minZ);

            Vector3[] face = pools.vector3ArrayPool.PopExact(4);
            BlockFace[] mask = pools.blockFaceArrayPool.PopExact(m_SideSize * m_SideSize);

            #region Top face

            if (listeners[(int)Direction.up] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.up) == 0 ||
                maxY != m_SideSize)
            {
                Array.Clear(mask, 0, mask.Length);

                // x axis - width
                // z axis - height

                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(minX, maxY, minZ, m_Pow);
                int zOffset = sizeWithPadding - maxX + minX;

                // Build the mask
                for (int zz = minZ; zz < maxZ; ++zz, neighborIndex += zOffset)
                {
                    n = minX + zz * m_SideSize;
                    for (int xx = minX; xx < maxX; ++xx, ++n, ++neighborIndex)
                    {
                        int currentIndex = neighborIndex - sizeWithPaddingPow2; // (xx, maxY-1, zz);
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.up,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.up),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int zz = minZ; zz < maxZ; ++zz)
                {
                    n = minX + zz * m_SideSize;
                    for (int xx = minX; xx < maxX;)
                    {
                        if (mask[n].block == null)
                        {
                            ++xx;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(xx, maxY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][0];
                            face[1] = new Vector3(xx, maxY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][1];
                            face[2] = new Vector3(xx + w, maxY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][2];
                            face[3] = new Vector3(xx + w, maxY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][3];
                        }
                        else
                        {
                            face[0] = new Vector3(xx, maxY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][1];
                            face[1] = new Vector3(xx + w, maxY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][2];
                            face[2] = new Vector3(xx + w, maxY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][3];
                            face[3] = new Vector3(xx, maxY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.up][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        xx += w;
                        n += w;
                    }
                }
            }

            #endregion

            #region Bottom face

            if (listeners[(int)Direction.down] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.down) == 0 ||
                minY != 0)
            {
                Array.Clear(mask, 0, mask.Length);

                // x axis - width
                // z axis - height

                int currentIndex = Helpers.GetChunkIndex1DFrom3D(minX, minY, minZ, m_Pow);
                int zOffset = sizeWithPadding - maxX + minX;

                // Build the mask
                for (int zz = minZ; zz < maxZ; ++zz, currentIndex += zOffset)
                {
                    n = minX + zz * m_SideSize;
                    for (int xx = minX; xx < maxX; ++xx, ++n, ++currentIndex)
                    {
                        int neighborIndex = currentIndex - sizeWithPaddingPow2;
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.down,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.down),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int zz = minZ; zz < maxZ; ++zz)
                {
                    n = minX + zz * m_SideSize;
                    for (int xx = minX; xx < maxX;)
                    {
                        if (mask[n].block == null)
                        {
                            ++xx;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(xx, minY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][0];
                            face[1] = new Vector3(xx, minY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][1];
                            face[2] = new Vector3(xx + w, minY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][2];
                            face[3] = new Vector3(xx + w, minY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][3];
                        }
                        else
                        {
                            face[0] = new Vector3(xx, minY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][1];
                            face[1] = new Vector3(xx + w, minY, zz + h) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][2];
                            face[2] = new Vector3(xx + w, minY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][3];
                            face[3] = new Vector3(xx, minY, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.down][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        xx += w;
                        n += w;
                    }
                }
            }

            #endregion

            #region Right face

            if (listeners[(int)Direction.east] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.east) == 0 ||
                maxX != m_SideSize)
            {
                Array.Clear(mask, 0, mask.Length);

                // y axis - height
                // z axis - width

                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(maxX, minY, minZ, m_Pow);
                int yOffset = sizeWithPaddingPow2 - (maxZ - minZ) * sizeWithPadding;

                // Build the mask
                for (int yy = minY; yy < maxY; ++yy, neighborIndex += yOffset)
                {
                    n = minZ + yy * m_SideSize;
                    for (int zz = minZ; zz < maxZ; ++zz, ++n, neighborIndex += sizeWithPadding)
                    {
                        int currentIndex = neighborIndex - 1;
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.east,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.east),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int yy = minY; yy < maxY; ++yy)
                {
                    n = minZ + yy * m_SideSize;
                    for (int zz = minZ; zz < maxZ;)
                    {
                        if (mask[n].block == null)
                        {
                            ++zz;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(maxX, yy, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][0];
                            face[1] = new Vector3(maxX, yy + h, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][1];
                            face[2] = new Vector3(maxX, yy + h, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][2];
                            face[3] = new Vector3(maxX, yy, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][3];
                        }
                        else
                        {
                            face[0] = new Vector3(maxX, yy + h, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][1];
                            face[1] = new Vector3(maxX, yy + h, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][2];
                            face[2] = new Vector3(maxX, yy, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][3];
                            face[3] = new Vector3(maxX, yy, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.east][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        zz += w;
                        n += w;
                    }
                }
            }

            #endregion

            #region Left face

            if (listeners[(int)Direction.west] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.west) == 0 ||
                minX != 0)
            {
                Array.Clear(mask, 0, mask.Length);

                // y axis - height
                // z axis - width

                int currentIndex = Helpers.GetChunkIndex1DFrom3D(minX, minY, minZ, m_Pow);
                int yOffset = sizeWithPaddingPow2 - (maxZ - minZ) * sizeWithPadding;

                // Build the mask
                for (int yy = minY; yy < maxY; ++yy, currentIndex += yOffset)
                {
                    n = minZ + yy * m_SideSize;
                    for (int zz = minZ; zz < maxZ; ++zz, ++n, currentIndex += sizeWithPadding)
                    {
                        int neighborIndex = currentIndex - 1;
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.west,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.west),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int yy = minY; yy < maxY; ++yy)
                {
                    n = minZ + yy * m_SideSize;
                    for (int zz = minZ; zz < maxZ;)
                    {
                        if (mask[n].block == null)
                        {
                            ++zz;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(minX, yy, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][0];
                            face[1] = new Vector3(minX, yy + h, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][1];
                            face[2] = new Vector3(minX, yy + h, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][2];
                            face[3] = new Vector3(minX, yy, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][3];
                        }
                        else
                        {
                            face[0] = new Vector3(minX, yy + h, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][1];
                            face[1] = new Vector3(minX, yy + h, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][2];
                            face[2] = new Vector3(minX, yy, zz + w) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][3];
                            face[3] = new Vector3(minX, yy, zz) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.west][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        zz += w;
                        n += w;
                    }
                }
            }

            #endregion

            #region Front face

            if (listeners[(int)Direction.north] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.north) == 0 ||
                maxZ != m_SideSize)
            {
                Array.Clear(mask, 0, mask.Length);

                // x axis - width
                // y axis - height

                int neighborIndex = Helpers.GetChunkIndex1DFrom3D(minX, minY, maxZ, m_Pow);
                int yOffset = sizeWithPaddingPow2 - maxX + minX;

                // Build the mask
                for (int yy = minY; yy < maxY; ++yy, neighborIndex += yOffset)
                {
                    n = minX + yy * m_SideSize;
                    for (int xx = minX; xx < maxX; ++xx, ++n, ++neighborIndex)
                    {
                        int currentIndex = neighborIndex - sizeWithPadding;
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.north,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.north),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int yy = minY; yy < maxY; ++yy)
                {
                    n = minX + yy * m_SideSize;
                    for (int xx = minX; xx < maxX;)
                    {
                        if (mask[n].block == null)
                        {
                            ++xx;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(xx, yy, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][0];
                            face[1] = new Vector3(xx, yy + h, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][1];
                            face[2] = new Vector3(xx + w, yy + h, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][2];
                            face[3] = new Vector3(xx + w, yy, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][3];
                        }
                        else
                        {
                            face[0] = new Vector3(xx, yy + h, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][1];
                            face[1] = new Vector3(xx + w, yy + h, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][2];
                            face[2] = new Vector3(xx + w, yy, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][3];
                            face[3] = new Vector3(xx, yy, maxZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.north][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        xx += w;
                        n += w;
                    }
                }
            }

            #endregion

            #region Back face

            if (listeners[(int)Direction.south] != null ||
                // Don't render faces on world's edges for chunks with no neighbor
                (SideMask & Side.south) == 0 ||
                minZ != 0)
            {
                Array.Clear(mask, 0, mask.Length);

                // x axis - width
                // y axis - height

                int currentIndex = Helpers.GetChunkIndex1DFrom3D(minX, minY, minZ, m_Pow);
                int yOffset = sizeWithPaddingPow2 - maxX + minX;

                // Build the mask
                for (int yy = minY; yy < maxY; ++yy, currentIndex += yOffset)
                {
                    n = minX + yy * m_SideSize;
                    for (int xx = minX; xx < maxX; ++xx, ++n, ++currentIndex)
                    {
                        int neighborIndex = currentIndex - sizeWithPadding;
                        Block neighborBlock = blocks.GetBlock(neighborIndex);

                        // Let's see whether we can merge faces
                        if (block.CanBuildFaceWith(neighborBlock))
                        {
                            mask[n] = new BlockFace
                            {
                                block = block,
                                pos = texturePos,
                                side = Direction.south,
                                light = BlockUtils.CalculateColors(chunk, currentIndex, Direction.south),
                                materialID = block.RenderMaterialID
                            };
                        }
                    }
                }

                // Build faces from the mask if it's possible
                for (int yy = minY; yy < maxY; ++yy)
                {
                    n = minX + yy * m_SideSize;
                    for (int xx = minX; xx < maxX;)
                    {
                        if (mask[n].block == null)
                        {
                            ++xx;
                            ++n;
                            continue;
                        }

                        // Compute width and height
                        w = 1;
                        h = 1;

                        // Build the face
                        bool rotated = mask[n].light.FaceRotationNecessary;
                        if (!rotated)
                        {
                            face[0] = new Vector3(xx, yy, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][0];
                            face[1] = new Vector3(xx, yy + h, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][1];
                            face[2] = new Vector3(xx + w, yy + h, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][2];
                            face[3] = new Vector3(xx + w, yy, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][3];
                        }
                        else
                        {
                            face[0] = new Vector3(xx, yy + h, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][1];
                            face[1] = new Vector3(xx + w, yy + h, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][2];
                            face[2] = new Vector3(xx + w, yy, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][3];
                            face[3] = new Vector3(xx, yy, minZ) * m_Scale +
                                      BlockUtils.PaddingOffsets[(int)Direction.south][0];
                        }

                        block.BuildFace(chunk, face, ref mask[n], rotated);

                        // Zero out the mask. We don't need to process the same fields again
                        for (l = 0; l < h; ++l)
                        {
                            maskIndex = n + l * m_SideSize;
                            for (k = 0; k < w; ++k, ++maskIndex)
                                mask[maskIndex] = new BlockFace();
                        }

                        xx += w;
                        n += w;
                    }
                }
            }

            #endregion

            pools.blockFaceArrayPool.Push(mask);
            pools.vector3ArrayPool.Push(face);
        }
    }
}
