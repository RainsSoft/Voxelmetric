using UnityEngine;
using Voxelmetric.Code.Common.Math;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Configurable
{
    [System.Serializable]
    public class Dev_RandomLayer : Dev_TerrainLayer
    {
        [SerializeField]
        private string m_BlockName;
        public string BlockName { get { return m_BlockName; } set { m_BlockName = value; } }
        [SerializeField]
        private float m_Chance;
        public float Chance { get { return m_Chance; } set { m_Chance = value; } }

        private BlockData m_BlockToPlace;

        protected override void SetUp(Dev_LayerConfig config)
        {
            Dev_Block block = m_World.BlockProvider.Dev_GetBlock(BlockName);
            m_BlockToPlace = new BlockData(block.Type, block.Solid);
        }

        public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
        {
            var lpos = new Vector3(chunk.Pos.x + x, heightSoFar + 1f, chunk.Pos.z);
            float posChance = Randomization.Random(lpos.GetHashCode(), 200);
            if (m_Chance > posChance)
                return heightSoFar + 1;

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
}
