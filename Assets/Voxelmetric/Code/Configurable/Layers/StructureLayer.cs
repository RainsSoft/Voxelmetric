using System;
using System.Globalization;
using UnityEngine;
using Voxelmetric.Code;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.Math;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Load_Resources;

[System.Obsolete("Use 'Dev_StructureLayer' instead.")]
public class StructureLayer : TerrainLayer
{
    protected GeneratedStructure m_Structure;
    private float m_Chance;

    protected override void SetUp(LayerConfig config)
    {
        // Config files for random layers MUST define these properties
        m_Chance = float.Parse(properties["chance"], CultureInfo.InvariantCulture);

        var structureType = Type.GetType(config.Structure + ", " + typeof(GeneratedStructure).Assembly, false);
        if (structureType == null)
        {
            Debug.LogError("Could not create structure " + config.Structure);
            return;
        }

        m_Structure = (GeneratedStructure)Activator.CreateInstance(structureType);
    }

    public override void Init(LayerConfig config)
    {
        m_Structure.Init(m_World);
    }

    public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        return heightSoFar;
    }

    public override float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        return heightSoFar;
    }

    public override void GenerateStructures(Chunk chunk, int layerIndex)
    {
        //if (chunk.pos.x!=-30 || chunk.pos.y!=30 || chunk.pos.z!=0) return;

        int minX = chunk.Pos.x;
        int maxX = chunk.Pos.x + Env.CHUNK_SIZE_1;
        int minZ = chunk.Pos.z;
        int maxZ = chunk.Pos.z + Env.CHUNK_SIZE_1;

        int structureID = 0;

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector3Int pos = new Vector3Int(x, 0, z);
                float chanceAtPos = Randomization.RandomPrecise(pos.GetHashCode(), 44);

                if (m_Chance > chanceAtPos)
                {
                    if (Randomization.RandomPrecise(pos.Add(1, 0, 0).GetHashCode(), 44) > chanceAtPos &&
                        Randomization.RandomPrecise(pos.Add(-1, 0, 0).GetHashCode(), 44) > chanceAtPos &&
                        Randomization.RandomPrecise(pos.Add(0, 0, 1).GetHashCode(), 44) > chanceAtPos &&
                        Randomization.RandomPrecise(pos.Add(0, 0, -1).GetHashCode(), 44) > chanceAtPos)
                    {
                        int xx = Helpers.Mod(x, Env.CHUNK_SIZE);
                        int zz = Helpers.Mod(z, Env.CHUNK_SIZE);
                        int height = Helpers.FastFloor(m_TerrainGen.GetTerrainHeightForChunk(chunk, xx, zz));

                        Vector3Int worldPos = new Vector3Int(x, height, z);
                        m_Structure.Build(chunk, structureID++, ref worldPos, this);
                    }
                }
            }
        }
    }
}
