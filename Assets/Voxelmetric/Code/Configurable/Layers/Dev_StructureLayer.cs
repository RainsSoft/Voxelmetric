using UnityEngine;
using Voxelmetric.Code.Core;

namespace Voxelmetric.Code.Configurable
{
    //TODO: Implement Structure Layer
    public class Dev_StructureLayer : Dev_TerrainLayer
    {
        [SerializeField]
        private float m_Chance;
        public float Chance { get { return m_Chance; } set { m_Chance = value; } }

        protected GeneratedStructure m_Structure;

        protected override void SetUp(Dev_LayerConfig config)
        {

        }

        public override void Init(Dev_LayerConfig config)
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
    }
}
