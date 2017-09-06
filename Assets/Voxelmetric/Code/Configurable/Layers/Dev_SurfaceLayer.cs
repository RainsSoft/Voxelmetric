using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Configurable
{
    public class Dev_SurfaceLayer : Dev_TerrainLayer
    {
        [SerializeField]
        private string m_BlockName;
        public string BlockName { get { return m_BlockName; } set { m_BlockName = value; } }

        private BlockData m_BlockToPlace;

        protected override void SetUp(Dev_LayerConfig config)
        {
            Dev_Block block = m_World.BlockProvider.Dev_GetBlock(BlockName);
            m_BlockToPlace = new BlockData(block.Type, block.Solid);
        }

        public override float GetHeight(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
        {
            return heightSoFar + 1;
        }

        public override float GenerateLayer(Chunk chunk, int layerIndex, int x, int z, float heightSoFar, float strength)
        {
            SetBlocks(chunk, x, z, (int)heightSoFar, (int)heightSoFar + 1, m_BlockToPlace);

            return heightSoFar;
        }
    }
}
