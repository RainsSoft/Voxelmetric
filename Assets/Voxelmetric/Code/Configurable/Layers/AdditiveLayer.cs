using System.Globalization;
using Voxelmetric.Code;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources;
using Voxelmetric.Code.Utilities.Noise;

[System.Obsolete("Use 'Dev_AdditiveLayer' instead.")]
public class AdditiveLayer : TerrainLayer
{
    private BlockData m_BlockToPlace;
    private int m_MinHeight;
    private int m_MaxHeight;
    private int m_Amplitude;

    protected override void SetUp(LayerConfig config)
    {
        // Config files for additive layers MUST define these properties
        Block block = m_World.BlockProvider.GetBlock(properties["blockName"]);
        m_BlockToPlace = new BlockData(block.Type, block.Solid);

        m_Noise.Frequency = 1f / float.Parse(properties["frequency"], CultureInfo.InvariantCulture); // Frequency in configs is in fast 1/frequency
        m_Noise.Gain = float.Parse(properties["exponent"], CultureInfo.InvariantCulture);
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
        noiseSIMD.Frequency = noise.Frequency;
        noiseSIMD.Gain = noise.Gain;
#endif
        m_MinHeight = int.Parse(properties["minHeight"], CultureInfo.InvariantCulture);
        m_MaxHeight = int.Parse(properties["maxHeight"], CultureInfo.InvariantCulture);

        m_Amplitude = m_MaxHeight - m_MinHeight;
    }

    public override void PreProcess(Chunk chunk, int layerIndex)
    {
        NoiseItem ni = chunk.Pools.NoiseItems[layerIndex];
        ni.noiseGen.SetInterpBitStep(Env.CHUNK_SIZE_WITH_PADDING, 2);
        ni.lookupTable = chunk.Pools.floatArrayPool.Pop(ni.noiseGen.Size * ni.noiseGen.Size);

#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
        float[] noiseSet = chunk.pools.FloatArrayPool.Pop(ni.noiseGen.Size * ni.noiseGen.Size * ni.noiseGen.Size);

        // Generate SIMD noise
        int offsetShift = Env.ChunkPow - ni.noiseGen.Step;
        int xStart = (chunk.pos.x * Env.ChunkSize) << offsetShift;
        int yStart = (chunk.pos.y * Env.ChunkSize) << offsetShift;
        int zStart = (chunk.pos.z * Env.ChunkSize) << offsetShift;
        float scaleModifier = 1 << ni.noiseGen.Step;
        noiseSIMD.Noise.FillNoiseSet(noiseSet, xStart, yStart, zStart, ni.noiseGen.Size, ni.noiseGen.Size, ni.noiseGen.Size, scaleModifier);

        // Generate a lookup table
        int i = 0;
        for (int z = 0; z < ni.noiseGen.Size; z++)
            for (int x = 0; x < ni.noiseGen.Size; x++)
                ni.lookupTable[i++] = NoiseUtilsSIMD.GetNoise(noiseSet, ni.noiseGen.Size, x, 0, z, amplitude, noise.Gain);

        chunk.pools.FloatArrayPool.Push(noiseSet);
#else
        int xOffset = chunk.Pos.x;
        int zOffset = chunk.Pos.z;

        // Generate a lookup table
        int i = 0;
        for (int z = 0; z < ni.noiseGen.Size; z++)
        {
            float zf = (z << ni.noiseGen.Step) + zOffset;

            for (int x = 0; x < ni.noiseGen.Size; x++)
            {
                float xf = (x << ni.noiseGen.Step) + xOffset;
                ni.lookupTable[i++] = NoiseUtils.GetNoise(m_Noise.Noise, xf, 0, zf, 1f, m_Amplitude, m_Noise.Gain);
            }
        }
#endif
    }

    public override void PostProcess(Chunk chunk, int layerIndex)
    {
        NoiseItem ni = chunk.Pools.NoiseItems[layerIndex];
        chunk.Pools.floatArrayPool.Push(ni.lookupTable);
    }

    public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        NoiseItem ni = chunk.Pools.NoiseItems[layerIndex];

        // Calculate height to add and sum it with the min height (because the height of this
        // layer should fluctuate between minHeight and minHeight+the max noise) and multiply
        // it by strength so that a fraction of the result that gets used can be decided
        float heightToAdd = ni.noiseGen.Interpolate(x, z, ni.lookupTable);
        heightToAdd += m_MinHeight;
        heightToAdd = heightToAdd * strength;

        return heightSoFar + heightToAdd;
    }

    public override float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
    {
        NoiseItem ni = chunk.Pools.NoiseItems[layerIndex];

        // Calculate height to add and sum it with the min height (because the height of this
        // layer should fluctuate between minHeight and minHeight+the max noise) and multiply
        // it by strength so that a fraction of the result that gets used can be decided
        float heightToAdd = ni.noiseGen.Interpolate(x, z, ni.lookupTable);
        heightToAdd += m_MinHeight;
        heightToAdd = heightToAdd * strength;

        SetBlocks(chunk, x, z, (int)heightSoFar, (int)(heightSoFar + heightToAdd), m_BlockToPlace);

        return heightSoFar + heightToAdd;
    }
}
