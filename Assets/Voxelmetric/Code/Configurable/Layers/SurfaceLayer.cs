using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources;

[System.Obsolete("Use 'Dev_SurfaceLayer' instead.")]
public class SurfaceLayer : TerrainLayer
{
    // Right now this acts just like additive layer but always set to one block thickness
    // but it's a placeholder so that in the future we can do things like blend surface layers
    // between separate biomes

    private BlockData m_BlockToPlace;

    protected override void SetUp(LayerConfig config)
    {
        Block block = m_World.BlockProvider.GetBlock(properties["blockName"]);
        m_BlockToPlace = new BlockData(block.Type, block.Solid);
    }

    public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        return heightSoFar + 1;
    }

    public override float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        SetBlocks(chunk, x, z, (int)heightSoFar, (int)heightSoFar + 1, m_BlockToPlace);

        return heightSoFar + 1;
    }
}
