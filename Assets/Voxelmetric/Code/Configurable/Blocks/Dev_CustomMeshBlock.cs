using UnityEngine;
using Voxelmetric.Code.Configurable;
using Voxelmetric.Code.Geometry.GeometryBatcher;
using Voxelmetric.Code.Load_Resources.Blocks;

namespace Voxelmetric.Code.Core
{
    public class Dev_CustomMeshBlock : Dev_Block
    {
        public Dev_CustomMeshBlockConfig CustomMeshConfig { get { return (Dev_CustomMeshBlockConfig)m_Config; } }

        public override void OnInit(BlockProvider blockProvider)
        {
            Custom = true;
        }

        public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
        {
            //TODO: Get texture
            //Rect texture = CustomMeshConfig.MeshTexture != null ? 

            RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
            batcher.UseColors(materialID);
            if (CustomMeshConfig.MeshTexture != null)
                batcher.UseTextures(materialID);

            //batcher.AddMeshData(CustomMeshConfig.Triangles, CustomMeshConfig.Vertices, ref Texture, localPos, materialID);
        }
    }
}
