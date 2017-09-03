using UnityEngine;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources.Textures;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Geometry.GeometryBatcher;

public class CubeBlock: Block
{
    public TextureCollection[] Textures
    {
        get { return ((CubeBlockConfig)m_Config).Textures; }
    }

    public override void BuildFace(Chunk chunk, Vector3[] vertices, ref BlockFace face, bool rotated)
    {
        bool backface = DirectionUtils.IsBackface(face.side);
        int d = DirectionUtils.Get(face.side);

        LocalPools pools = chunk.Pools;
        VertexData[] vertexData = pools.vertexDataArrayPool.PopExact(4);
        {
            if (vertices==null)
            {
                Vector3 pos = face.pos;
                vertexData[0].vertex = pos + BlockUtils.PaddingOffsets[d][0];
                vertexData[1].vertex = pos + BlockUtils.PaddingOffsets[d][1];
                vertexData[2].vertex = pos + BlockUtils.PaddingOffsets[d][2];
                vertexData[3].vertex = pos + BlockUtils.PaddingOffsets[d][3];
            }
            else
            {
                vertexData[0].vertex = vertices[0];
                vertexData[1].vertex = vertices[1];
                vertexData[2].vertex = vertices[2];
                vertexData[3].vertex = vertices[3];
            }

            BlockUtils.PrepareTexture(chunk, ref face.pos, vertexData, face.side, Textures, rotated);
            BlockUtils.PrepareColors(chunk, vertexData, ref face.light);

            RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
            batcher.UseColors(face.materialID);
            batcher.UseTextures(face.materialID);
            batcher.AddFace(vertexData, backface, face.materialID);
        }
        pools.vertexDataArrayPool.Push(vertexData);
    }
}
