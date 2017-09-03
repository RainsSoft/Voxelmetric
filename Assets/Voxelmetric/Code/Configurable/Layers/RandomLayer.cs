using System.Globalization;
using UnityEngine;
using Voxelmetric.Code.Common.Math;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources;

public class RandomLayer : TerrainLayer
{
    private BlockData m_BlockToPlace;
    private float m_Chance;

    protected override void SetUp(LayerConfig config)
    {
        // Config files for random layers MUST define these properties
        Block block = m_World.BlockProvider.GetBlock(properties["blockName"]);
        m_BlockToPlace = new BlockData(block.Type, block.Solid);

        m_Chance = float.Parse(properties["chance"], CultureInfo.InvariantCulture);
    }

    public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        var lpos = new Vector3(chunk.Pos.x + x, heightSoFar + 1f, chunk.Pos.z);
        float posChance = Randomization.Random(lpos.GetHashCode(), 200);

        if (m_Chance > posChance)
        {
            return heightSoFar + 1;
        }

        return heightSoFar;
    }

    public override float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        var lpos = new Vector3(chunk.Pos.x + x, heightSoFar + 1f, chunk.Pos.z);
        float posChance = Randomization.Random(lpos.GetHashCode(), 200);

        if (m_Chance > posChance)
        {
            SetBlocks(chunk, x, z, (int)heightSoFar, (int)(heightSoFar + 1f), m_BlockToPlace);

            return heightSoFar + 1;
        }

        return heightSoFar;
    }
}
