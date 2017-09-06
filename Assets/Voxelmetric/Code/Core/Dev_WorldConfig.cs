using System.Collections.Generic;
using UnityEngine;

namespace Voxelmetric.Code.Configurable
{
    [CreateAssetMenu(fileName = "New World Config", menuName = "Voxelmetric/World Config")]
    public class Dev_WorldConfig : ScriptableObject
    {
        [SerializeField]
        private List<Dev_BlockConfig> m_Blocks = new List<Dev_BlockConfig>();
        public List<Dev_BlockConfig> Blocks { get { return m_Blocks; } set { m_Blocks = value; } }
        [SerializeField]
        private List<Dev_LayerConfig> m_Layers = new List<Dev_LayerConfig>();
        public List<Dev_LayerConfig> Layers { get { return m_Layers; } set { m_Layers = value; } }

        [Space]

        [SerializeField]
        private int m_MinX = -2;
        public int MinX { get { return m_MinX; } set { m_MinX = value; } }
        [SerializeField]
        private int m_MaxX = 2;
        public int MaxX { get { return m_MaxX; } set { m_MaxX = value; } }
        [SerializeField]
        private int m_MinY = -2;
        public int MinY { get { return m_MinY; } set { m_MinY = value; } }
        [SerializeField]
        private int m_MaxY = 2;
        public int MaxY { get { return m_MaxY; } set { m_MaxY = value; } }
        [SerializeField]
        private int m_MinZ = -2;
        public int MinZ { get { return m_MinZ; } set { m_MinZ = value; } }
        [SerializeField]
        private int m_MaxZ = 2;
        public int MaxZ { get { return m_MaxZ; } set { m_MaxZ = value; } }

        [Space]

        [SerializeField]
        private bool m_AaddAOToMesh = true;
        public bool AddAOToMesh { get { return m_AaddAOToMesh; } set { m_AaddAOToMesh = value; } }
        [SerializeField]
        [Range(0, 1)]
        private float m_AmbientOcclusionStrength = 0.8f;
        public float AmbientOcclusionStrength { get { return m_AmbientOcclusionStrength; } set { m_AmbientOcclusionStrength = value; } }

        [Space]

        [SerializeField]
        private bool m_UseMipMaps = true;
        public bool UseMipMaps { get { return m_UseMipMaps; } set { m_UseMipMaps = value; } }
        [SerializeField]
        private TextureFormat m_TextureFormat = TextureFormat.ARGB32;
        public TextureFormat TextureFormat { get { return m_TextureFormat; } set { m_TextureFormat = value; } }
        [SerializeField]
        private FilterMode m_TextureAtlasFiltering = FilterMode.Point;
        public FilterMode TextureAtlasFiltering { get { return m_TextureAtlasFiltering; } set { m_TextureAtlasFiltering = value; } }
        [SerializeField]
        private int m_TextureAtlasPadding = 32;
        public int TextureAtlasPadding { get { return m_TextureAtlasPadding; } set { m_TextureAtlasPadding = value; } }

        public Texture2D[] GetAllTextures()
        {
            List<Texture2D> textures = new List<Texture2D>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                Texture2D[] blockTextures = Blocks[i].GetTextures();
                for (int j = 0; j < blockTextures.Length; j++)
                {
                    if (!textures.Contains(blockTextures[j]))
                        textures.Add(blockTextures[j]);
                }
            }

            return textures.ToArray();
        }
    }
}
