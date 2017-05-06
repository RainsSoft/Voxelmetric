﻿using UnityEngine;
using Voxelmetric.Code;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources.Textures;
using Voxelmetric.Code.Rendering;
using Voxelmetric.Code.Rendering.GeometryBatcher;

public class CrossMeshBlock : Block
{
    private static readonly float coef = 1.0f / 64.0f;

    public TextureCollection texture { get { return ((CrossMeshBlockConfig)Config).texture; } }

    public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
    {
        LocalPools pools = chunk.pools;
        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;

        // Using the block positions hash is much better for random numbers than saving the offset and height in the block data
        int hash = localPos.GetHashCode();
        if (hash<0)
            hash *= -1;

        float blockHeight = (hash&63)*coef*Env.BlockSize;

        hash *= 39;
        if (hash<0)
            hash *= -1;

        float offsetX = (hash&63)*coef*Env.BlockSizeHalf-Env.BlockSizeHalf*0.5f;

        hash *= 39;
        if (hash<0)
            hash *= -1;

        float offsetZ = (hash&63)*coef*Env.BlockSizeHalf-Env.BlockSizeHalf*0.5f;

        // Converting the position to a vector adjusts it based on block size and gives us real world coordinates for x, y and z
        Vector3 vPos = localPos;
        vPos += new Vector3(offsetX, 0, offsetZ);

        float x1 = vPos.x-BlockUtils.blockPadding-Env.BlockSizeHalf;
        float x2 = vPos.x+BlockUtils.blockPadding+Env.BlockSizeHalf;
        float y1 = vPos.y-BlockUtils.blockPadding;
        float y2 = vPos.y+BlockUtils.blockPadding+blockHeight;
        float z1 = vPos.z-BlockUtils.blockPadding-Env.BlockSizeHalf;
        float z2 = vPos.z+BlockUtils.blockPadding+Env.BlockSizeHalf;

        VertexData[] vertexData = pools.VertexDataArrayPool.PopExact(4);
        {
            vertexData[0].Vertex = new Vector3(x1, y1, z2);
            vertexData[1].Vertex = new Vector3(x1, y2, z2);
            vertexData[2].Vertex = new Vector3(x2, y2, z1);
            vertexData[3].Vertex = new Vector3(x2, y1, z1);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, texture);
            BlockUtils.SetColors(vertexData, 1, 1, 1, 1, 1);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].Vertex = new Vector3(x2, y1, z1);
            vertexData[1].Vertex = new Vector3(x2, y2, z1);
            vertexData[2].Vertex = new Vector3(x1, y2, z2);
            vertexData[3].Vertex = new Vector3(x1, y1, z2);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, texture);
            BlockUtils.SetColors(vertexData, 1, 1, 1, 1, 1);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].Vertex = new Vector3(x2, y1, z2);
            vertexData[1].Vertex = new Vector3(x2, y2, z2);
            vertexData[2].Vertex = new Vector3(x1, y2, z1);
            vertexData[3].Vertex = new Vector3(x1, y1, z1);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, texture);
            BlockUtils.SetColors(vertexData, 1, 1, 1, 1, 1);
            batcher.AddFace(vertexData, false, materialID);
        }
        {
            vertexData[0].Vertex = new Vector3(x1, y1, z1);
            vertexData[1].Vertex = new Vector3(x1, y2, z1);
            vertexData[2].Vertex = new Vector3(x2, y2, z2);
            vertexData[3].Vertex = new Vector3(x2, y1, z2);
            BlockUtils.PrepareTexture(chunk, ref localPos, vertexData, Direction.north, texture);
            BlockUtils.SetColors(vertexData, 1, 1, 1, 1, 1);
            batcher.AddFace(vertexData, false, materialID);
        }
        pools.VertexDataArrayPool.Push(vertexData);
    }
}
