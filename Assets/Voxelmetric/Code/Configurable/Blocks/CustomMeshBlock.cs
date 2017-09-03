using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry.GeometryBatcher;
using Voxelmetric.Code.Load_Resources.Blocks;

public class CustomMeshBlock : Block
{
    public CustomMeshBlockConfig CustomMeshConfig { get { return (CustomMeshBlockConfig)m_Config; } }

    public override void OnInit(BlockProvider blockProvider)
    {
        Custom = true;
    }

    public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
    {
        Rect texture = CustomMeshConfig.Texture != null ? CustomMeshConfig.Texture.GetTexture(chunk, ref localPos, Direction.down) : new Rect();

        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
        batcher.UseColors(materialID);
        if (CustomMeshConfig.Texture != null)
            batcher.UseTextures(materialID);

        batcher.AddMeshData(CustomMeshConfig.Tris, CustomMeshConfig.Verts, ref texture, localPos, materialID);
    }
}
