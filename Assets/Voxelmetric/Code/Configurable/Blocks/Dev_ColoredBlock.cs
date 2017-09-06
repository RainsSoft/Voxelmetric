using UnityEngine;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Configurable;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Geometry.GeometryBatcher;

namespace Voxelmetric.Code.Core
{
    public class Dev_ColoredBlock : Dev_Block
    {
        public BlockColors BlockColors { get { return m_Config.Colors; } }

        public override void BuildFace(Chunk chunk, Vector3[] vertices, ref BlockFace face, bool rotated)
        {
            bool backFace = DirectionUtils.IsBackface(face.side);
            int d = DirectionUtils.Get(face.side);

            LocalPools pools = chunk.Pools;
            VertexData[] vertexData = pools.vertexDataArrayPool.PopExact(4);

            if (vertices == null)
            {
                Vector3 pos = face.pos;

                vertexData[0].vertex = pos + BlockUtils.PaddingOffsets[d][0];
                vertexData[0].color = BlockColors.GetColorFromDirection(d);
                vertexData[0].uv = Vector2.zero;

                vertexData[1].vertex = pos + BlockUtils.PaddingOffsets[d][1];
                vertexData[1].color = BlockColors.GetColorFromDirection(d);
                vertexData[1].uv = Vector2.zero;

                vertexData[2].vertex = pos + BlockUtils.PaddingOffsets[d][2];
                vertexData[2].color = BlockColors.GetColorFromDirection(d);
                vertexData[2].uv = Vector2.zero;

                vertexData[3].vertex = pos + BlockUtils.PaddingOffsets[d][3];
                vertexData[3].color = BlockColors.GetColorFromDirection(d);
                vertexData[3].uv = Vector2.zero;
            }
            else
            {
                vertexData[0].vertex = vertices[0];
                vertexData[0].color = BlockColors.GetColorFromDirection(d);
                vertexData[0].uv = Vector2.zero;

                vertexData[1].vertex = vertices[1];
                vertexData[1].color = BlockColors.GetColorFromDirection(d);
                vertexData[1].uv = Vector2.zero;

                vertexData[2].vertex = vertices[2];
                vertexData[2].color = BlockColors.GetColorFromDirection(d);
                vertexData[2].uv = Vector2.zero;

                vertexData[3].vertex = vertices[3];
                vertexData[3].color = BlockColors.GetColorFromDirection(d);
                vertexData[3].uv = Vector2.zero;
            }

            BlockUtils.AdjustColors(chunk, vertexData, face.light);

            RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
            batcher.UseColors(face.materialID);
            batcher.AddFace(vertexData, backFace, face.materialID);

            pools.vertexDataArrayPool.Push(vertexData);
        }
    }
}
