using UnityEngine;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Configurable.Blocks.Utilities;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Geometry.GeometryBatcher;

public class MagicaMeshBlock : Block
{
    public MagicaMeshBlockConfig MagicMeshConfig { get { return (MagicaMeshBlockConfig)m_Config; } }

    public override void BuildFace(Chunk chunk, Vector3[] vertices, ref BlockFace face, bool rotated)
    {
        bool backFace = DirectionUtils.IsBackface(face.side);

        LocalPools pools = chunk.Pools;
        VertexData[] vertexData = pools.vertexDataArrayPool.PopExact(4);
        {
            vertexData[0].vertex = vertices[0];
            vertexData[0].color = Color.white;
            vertexData[0].uv = Vector2.zero;

            vertexData[1].vertex = vertices[1];
            vertexData[1].color = Color.white;
            vertexData[1].uv = Vector2.zero;

            vertexData[2].vertex = vertices[2];
            vertexData[2].color = Color.white;
            vertexData[2].uv = Vector2.zero;

            vertexData[3].vertex = vertices[3];
            vertexData[3].color = Color.white;
            vertexData[3].uv = Vector2.zero;

            BlockUtils.AdjustColors(chunk, vertexData, face.light);
            MagicMeshConfig.AddFace(vertexData, backFace);
        }
        pools.vertexDataArrayPool.Push(vertexData);
    }

    public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
    {
        Rect rect = new Rect();

        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
        batcher.UseColors(materialID);

        batcher.AddMeshData(MagicMeshConfig.Tris, MagicMeshConfig.Verts, ref rect, localPos, materialID);
    }
}
