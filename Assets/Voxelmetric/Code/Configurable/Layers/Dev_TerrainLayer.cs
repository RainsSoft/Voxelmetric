using System;
using System.Collections.Generic;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Utilities.Noise;

namespace Voxelmetric.Code.Configurable
{
    public abstract class Dev_TerrainLayer : IComparable, IEquatable<Dev_TerrainLayer>
    {
        protected World m_World;
        protected TerrainGen m_TerrainGen;
        protected readonly Dictionary<string, string> properties = new Dictionary<string, string>();
        protected NoiseWrapper m_Noise;
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
        protected NoiseWrapperSIMD noiseSIMD;
#endif

        private string m_LayerName;
        public string LayerName { get { return m_LayerName; } set { m_LayerName = value; } }
        private int m_Index;
        public int Index { get { return m_Index; } set { m_Index = value; } }
        private bool m_IsStructure;
        public bool IsStructure { get { return m_IsStructure; } set { m_IsStructure = value; } }

        public NoiseWrapper Noise { get { return m_Noise; } }
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
        public NoiseWrapperSIMD NoiseSIMD { get { return noiseSIMD; } }
#endif

        public void BaseSetUp(Dev_LayerConfig config, World world, TerrainGen terrainGen)
        {
            m_TerrainGen = terrainGen;
            m_LayerName = config.LayerName;
            m_World = world;
            Index = config.Index;

            //TODO: Implement structure

            m_Noise = new NoiseWrapper(world.name);
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && ENABLE_FASTSIMD
            noiseSIMD = new NoiseWrapperSIMD(world.name);
#endif

            foreach (var key in config.Properties.Keys)
            {
                properties.Add(key.ToString(), config.Properties[key].ToString());
            }

            SetUp(config);
        }

        protected virtual void SetUp(Dev_LayerConfig config) { }

        public virtual void Init(Dev_LayerConfig config) { }

        public virtual void PreProcess(Chunk chunk, int layerIndex) { }

        public virtual void PostProcess(Chunk chunk, int layerIndex) { }

        public abstract float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength);

        public abstract float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength);

        public virtual void GenerateStructures(Chunk chunk, int layerIndex) { }

        protected static void SetBlocks(Chunk chunk, int x, int z, int startPlaceHeight, int endPlaceHeight, BlockData blockData)
        {
            int chunkY = chunk.Pos.y;

            int yMax = chunkY + Env.CHUNK_SIZE;
            if (startPlaceHeight >= yMax || endPlaceHeight < chunkY)
                return;

            int y = startPlaceHeight;
            if (startPlaceHeight < chunkY)
                y = chunkY;

            ChunkBlocks blocks = chunk.Blocks;
            while (y < yMax)
            {
                blocks.SetRaw(Helpers.GetChunkIndex1DFrom3D(x, y - chunkY, z), blockData);
                y++;
            }
        }

        public int CompareTo(object obj)
        {
            return Index.CompareTo(((Dev_TerrainLayer)obj).Index);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Dev_TerrainLayer))
                return false;
            Dev_TerrainLayer other = (Dev_TerrainLayer)obj;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return Index.GetHashCode();
        }

        public bool Equals(Dev_TerrainLayer other)
        {
            return other.Index == Index;
        }
    }
}
