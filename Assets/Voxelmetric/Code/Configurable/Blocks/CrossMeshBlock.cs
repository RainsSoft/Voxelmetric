using UnityEngine;
using Voxelmetric.Code;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Geometry.GeometryBatcher;
using Voxelmetric.Code.Load_Resources.Blocks;
using Voxelmetric.Code.Load_Resources.Textures;

[System.Obsolete("Use 'Dev_CrossMeshBlock' instead.")]
public class CrossMeshBlock : Block
{
    private static readonly float coef = 1.0f / 64.0f;

    public TextureCollection Texture { get { return ((CrossMeshBlockConfig)m_Config).Texture; } }

    public override void OnInit(BlockProvider blockProvider)
    {
        Custom = true;
    }

    public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
    {
        LocalPools pools = chunk.Pools;
        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
        batcher.UseTextures(materialID);

        // Using the block positions hash is much better for random numbers than saving the offset and height in the block data
        int hash = localPos.GetHashCode();

        float blockHeight = (hash & 63) * coef * Env.BLOCK_SIZE;

        hash *= 39;
        float offsetX = (hash & 63) * coef * Env.BLOCK_SIZE_HALF - Env.BLOCK_SIZE_HALF * 0.5f;

        hash *= 39;
        float offsetZ = (hash & 63) * coef * Env.BLOCK_SIZE_HALF - Env.BLOCK_SIZE_HALF * 0.5f;

        // Converting the position to a vector adjusts it based on block size and gives us real world coordinates for x, y and z
        Vector3 vPos = localPos;
        vPos += new Vector3(offsetX, 0, offsetZ);

        float x1 = vPos.x - BlockUtils.blockPadding;
        float x2 = vPos.x + BlockUtils.blockPadding + Env.BLOCK_SIZE;
        float y1 = vPos.y - BlockUtils.blockPadding;
        float y2 = vPos.y + BlockUtils.blockPadding + blockHeight;
        float z1 = vPos.z - BlockUtils.blockPadding;
        float z2 = vPos.z + BlockUtils.blockPadding + Env.BLOCK_SIZE;

        VertexData[] vertexData = pools.vertexDataArrayPool.PopExact(4);
        {
            vertexData[0].vertex = new Vector3(x1, y1, z2);
            vertexData[1].vertex = new Vector3(x1, y2, z2);
            vertexData[2].vertex = new Vector3(x2, y2, z1);
            vertexData[3].vertex = new Vector3(x2, y1, z1);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, Texture, false);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].vertex = new Vector3(x2, y1, z1);
            vertexData[1].vertex = new Vector3(x2, y2, z1);
            vertexData[2].vertex = new Vector3(x1, y2, z2);
            vertexData[3].vertex = new Vector3(x1, y1, z2);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, Texture, false);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].vertex = new Vector3(x2, y1, z2);
            vertexData[1].vertex = new Vector3(x2, y2, z2);
            vertexData[2].vertex = new Vector3(x1, y2, z1);
            vertexData[3].vertex = new Vector3(x1, y1, z1);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, Texture, false);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].vertex = new Vector3(x1, y1, z1);
            vertexData[1].vertex = new Vector3(x1, y2, z1);
            vertexData[2].vertex = new Vector3(x2, y2, z2);
            vertexData[3].vertex = new Vector3(x2, y1, z2);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, Texture, false);
            batcher.AddFace(vertexData, false, materialID);
        }
        pools.vertexDataArrayPool.Push(vertexData);
    }
}
