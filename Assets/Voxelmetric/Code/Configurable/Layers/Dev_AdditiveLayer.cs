using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Utilities.Noise;

namespace Voxelmetric.Code.Configurable
{
    [System.Serializable]
    public class Dev_AdditiveLayer : Dev_TerrainLayer
    {
        [SerializeField]
        private string m_BlockName;
        public string BlockName { get { return m_BlockName; } set { m_BlockName = value; } }
        [SerializeField]
        private float m_Frequency;
        public float Frequency { get { return m_Frequency; } set { m_Frequency = value; } }
        [SerializeField]
        private float m_Exponent;
        public float Exponent { get { return m_Exponent; } set { m_Exponent = value; } }

        [SerializeField]
        private int m_MinHeight;
        public int MinHeight { get { return m_MinHeight; } set { m_MinHeight = value; } }
        [SerializeField]
        private int m_MaxHeight;
        public int MaxHeight { get { return m_MaxHeight; } set { m_MaxHeight = value; } }

        private int m_Amplitude;

        private BlockData m_BlockToPlace;

        protected override void SetUp(Dev_LayerConfig config)
        {
            // Config files for additive layers MUST define these properties
            Dev_Block block = m_World.BlockProvider.Dev_GetBlock(BlockName);
            m_BlockToPlace = new BlockData(block.Type, block.Solid);

            m_Noise.Frequency = 1f / Frequency; // Frequency in configs is in fast 1/frequency
            m_Noise.Gain = Exponent;
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
            noiseSIMD.Frequency = noise.Frequency;
            noiseSIMD.Gain = noise.Gain;
#endif
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
}
