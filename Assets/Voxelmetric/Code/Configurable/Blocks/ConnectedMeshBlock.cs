using UnityEngine;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry.GeometryBatcher;
using Voxelmetric.Code.Load_Resources.Blocks;

[System.Obsolete("Use 'Dev_ConnectedMeshBlock' instead.")]
public class ConnectedMeshBlock : CustomMeshBlock
{
    public ConnectedMeshBlockConfig ConnectedMeshConfig { get { return (ConnectedMeshBlockConfig)m_Config; } }

    public override void OnInit(BlockProvider blockProvider)
    {
        Custom = true;

        if (ConnectedMeshConfig.ConnectsToTypes == null)
        {
            ConnectedMeshConfig.ConnectsToTypes = new int[ConnectedMeshConfig.ConnectsToNames.Length];
            for (int i = 0; i < ConnectedMeshConfig.ConnectsToNames.Length; i++)
            {
                ConnectedMeshConfig.ConnectsToTypes[i] = blockProvider.GetType(ConnectedMeshConfig.ConnectsToNames[i]);
            }
        }
    }

    public override void BuildFace(Chunk chunk, Vector3[] vertices, ref BlockFace face, bool rotated)
    {
        var tris = ConnectedMeshConfig.directionalTris[(int)face.side];
        if (tris == null)
            return;

        var verts = ConnectedMeshConfig.directionalVerts[(int)face.side];
        var texture = ConnectedMeshConfig.Texture;

        Rect rect;
        ChunkBlocks blocks = chunk.Blocks;

        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
        batcher.UseTextures(face.materialID);

        Vector3Int sidePos = face.pos.Add(face.side);
        if (ConnectedMeshConfig.ConnectsToSolid && blocks.Get(ref sidePos).Solid)
        {
            rect = ConnectedMeshConfig.Texture.GetTexture(chunk, ref face.pos, face.side);
            batcher.AddMeshData(tris, verts, ref rect, face.pos, face.materialID);
        }
        else if (ConnectedMeshConfig.ConnectsToTypes.Length != 0)
        {
            int neighborType = blocks.Get(ref sidePos).Type;
            for (int i = 0; i < ConnectedMeshConfig.ConnectsToTypes.Length; i++)
            {
                if (neighborType == ConnectedMeshConfig.ConnectsToTypes[i])
                {
                    rect = texture.GetTexture(chunk, ref face.pos, face.side);
                    batcher.AddMeshData(tris, verts, ref rect, face.pos, face.materialID);
                    break;
                }
            }
        }

        rect = CustomMeshConfig.Texture.GetTexture(chunk, ref face.pos, Direction.down);
        batcher.AddMeshData(CustomMeshConfig.Tris, CustomMeshConfig.Verts, ref rect, face.pos, face.materialID);
    }

    public override void BuildBlock(Chunk chunk, ref Vector3Int localPos, int materialID)
    {
        for (int d = 0; d < 6; d++)
        {
            Direction dir = DirectionUtils.Get(d);

            BlockFace face = new BlockFace()
            {
                block = null,
                pos = localPos,
                side = dir,
                light = new BlockLightData(0),
                materialID = materialID
            };

            BuildFace(chunk, null, ref face, false);
        }

        RenderGeometryBatcher batcher = chunk.GeometryHandler.Batcher;
        batcher.UseTextures(materialID);

        Rect texture = CustomMeshConfig.Texture.GetTexture(chunk, ref localPos, Direction.down);
        batcher.AddMeshData(CustomMeshConfig.Tris, CustomMeshConfig.Verts, ref texture, localPos, materialID);
    }
}
