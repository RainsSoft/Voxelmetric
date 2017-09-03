using UnityEngine;

namespace Voxelmetric.Code.Load_Resources.Textures
{
    public struct TextureConfig
    {

        private string m_Name;
        public string Name { get { return m_Name; } set { m_Name = value; } }

        private bool m_ConnectedTextures;
        public bool ConnectedTextures { get { return m_ConnectedTextures; } set { m_ConnectedTextures = value; } }
        private bool m_RandomTextures;
        public bool RandomTextures { get { return m_RandomTextures; } set { m_RandomTextures = value; } }

        private Texture[] m_Textures;
        public Texture[] Textures { get { return m_Textures; } set { m_Textures = value; } }

        public struct Texture
        {
            public string file;
            public Texture2D texture2d;
            public int connectedType;
            public int weight;

            public int xPos;
            public int yPos;
            public int width;
            public int height;

            public bool repeatingTexture;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
