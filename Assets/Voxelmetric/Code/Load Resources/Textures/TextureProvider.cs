using System.Collections.Generic;
using UnityEngine;

namespace Voxelmetric.Code.Load_Resources.Textures
{
    public class TextureProvider
    {
        private WorldConfig m_Config;
        private TextureConfig[] m_Configs;

        //! Texture atlas
        public readonly Dictionary<string, TextureCollection> textures;
        //! Texture atlas
        private Texture2D m_Atlas;
        public Texture2D Atlas { get { return m_Atlas; } set { m_Atlas = value; } }

        public static TextureProvider Create()
        {
            return new TextureProvider();
        }

        private TextureProvider()
        {
            textures = new Dictionary<string, TextureCollection>();
        }

        public void Init(WorldConfig config)
        {
            this.m_Config = config;
            LoadTextureIndex();
        }

        private void LoadTextureIndex()
        {
            // If you're using a pre defined texture atlas return now, don't try to generate a new one
            if (m_Config.useCustomTextureAtlas)
            {
                UseCustomTextureAtlas();
                return;
            }

            if (m_Configs == null)
                m_Configs = LoadAllTextures();

            List<Texture2D> individualTextures = new List<Texture2D>();
            for (int i = 0; i < m_Configs.Length; i++)
            {
                for (int j = 0; j < m_Configs[i].Textures.Length; j++)
                {
                    //create an array of all these textures
                    individualTextures.Add(m_Configs[i].Textures[j].texture2d);
                }
            }

            // Generate atlas
            Texture2D packedTextures = new Texture2D(8192, 8192);
            Rect[] rects = packedTextures.PackTextures(individualTextures.ToArray(), m_Config.textureAtlasPadding, 8192, false);

            // Transfer over the pixels to another texture2d because PackTextures resets the texture format and useMipMaps settings
            Atlas = new Texture2D(packedTextures.width, packedTextures.height, m_Config.textureFormat, m_Config.useMipMaps);
            Atlas.SetPixels(packedTextures.GetPixels(0, 0, packedTextures.width, packedTextures.height));
            Atlas.filterMode = m_Config.textureAtlasFiltering;

            List<Rect> repeatingTextures = new List<Rect>();
            List<Rect> nonrepeatingTextures = new List<Rect>();

            int index = 0;
            for (int i = 0; i < m_Configs.Length; i++)
            {
                for (int j = 0; j < m_Configs[i].Textures.Length; j++)
                {
                    Rect texture = rects[index];

                    TextureCollection collection;
                    if (!textures.TryGetValue(m_Configs[i].Name, out collection))
                    {
                        collection = new TextureCollection(m_Configs[i].Name);
                        textures.Add(m_Configs[i].Name, collection);
                    }

                    int connectedTextureType = -1;
                    if (m_Configs[i].ConnectedTextures)
                    {
                        connectedTextureType = m_Configs[i].Textures[j].connectedType;
                    }


                    collection.AddTexture(texture, connectedTextureType, m_Configs[i].Textures[j].weight);

                    if (m_Configs[i].Textures[j].repeatingTexture)
                        repeatingTextures.Add(rects[index]);
                    else
                        nonrepeatingTextures.Add(rects[index]);

                    index++;
                }
            }

            uPaddingBleed.BleedEdges(Atlas, m_Config.textureAtlasPadding, repeatingTextures.ToArray(), true);
            uPaddingBleed.BleedEdges(Atlas, m_Config.textureAtlasPadding, nonrepeatingTextures.ToArray(), false);
        }

        //This function is used if you've made your own texture atlas and the configs just specify where the textures are
        private void UseCustomTextureAtlas()
        {
            Atlas = Resources.Load<Texture2D>(m_Config.customTextureAtlasFile);

            m_Configs = new ConfigLoader<TextureConfig>(new[] { m_Config.textureFolder }).AllConfigs();

            for (int i = 0; i < m_Configs.Length; i++)
            {
                var cfg = m_Configs[i];

                for (int j = 0; j < cfg.Textures.Length; j++)
                {
                    var cfgTextures = cfg.Textures[j];

                    Rect texture = new Rect(
                        cfgTextures.xPos / (float)Atlas.width,
                        cfgTextures.yPos / (float)Atlas.height,
                        cfgTextures.width / (float)Atlas.width,
                        cfgTextures.height / (float)Atlas.height
                    );

                    TextureCollection collection;
                    if (!textures.TryGetValue(cfg.Name, out collection))
                    {
                        collection = new TextureCollection(cfg.Name);
                        textures.Add(cfg.Name, collection);
                    }

                    int connectedTextureType = -1;
                    if (cfg.ConnectedTextures)
                        connectedTextureType = m_Configs[i].Textures[j].connectedType;

                    collection.AddTexture(texture, connectedTextureType, m_Configs[i].Textures[j].weight);
                }
            }
        }

        private TextureConfig[] LoadAllTextures()
        {
            TextureConfig[] allConfigs = new ConfigLoader<TextureConfig>(new[] { m_Config.textureFolder }).AllConfigs();

            // Load all files in Textures folder
            Texture2D[] sourceTextures = Resources.LoadAll<Texture2D>(m_Config.textureFolder);

            Dictionary<string, Texture2D> sourceTexturesLookup = new Dictionary<string, Texture2D>();
            foreach (var texture in sourceTextures)
                sourceTexturesLookup.Add(texture.name, texture);

            for (int i = 0; i < allConfigs.Length; i++)
            {
                var cfg = allConfigs[i];

                for (int n = 0; n < cfg.Textures.Length; n++)
                    cfg.Textures[n].texture2d = Texture2DFromConfig(cfg.Textures[n], sourceTexturesLookup);

                if (cfg.ConnectedTextures)
                {
                    // Create all 48 possibilities from the 5 supplied textures
                    Texture2D[] newTextures = ConnectedTextures.ConnectedTexturesFromBaseTextures(cfg.Textures);
                    TextureConfig.Texture[] connectedTextures = new TextureConfig.Texture[48];

                    for (int x = 0; x < newTextures.Length; x++)
                    {
                        connectedTextures[x].connectedType = x;
                        connectedTextures[x].texture2d = newTextures[x];
                    }

                    cfg.Textures = connectedTextures;
                }
            }

            return allConfigs;
        }

        private Texture2D Texture2DFromConfig(TextureConfig.Texture texture, Dictionary<string, Texture2D> sourceTexturesLookup)
        {
            Texture2D file;
            if (!sourceTexturesLookup.TryGetValue(texture.file, out file))
            {
                Debug.LogError("Config referred to nonexistent file: " + texture.file);
                return null;
            }

            //No width or height means this texture is the whole file
            if (texture.width == 0 && texture.height == 0)
                return file;

            //If theres a width and a height fetch the pixels specified by the rect as a texture
            Texture2D newTexture = new Texture2D(texture.width, texture.height, m_Config.textureFormat, file.mipmapCount < 1);
            newTexture.SetPixels(0, 0, texture.width, texture.height, file.GetPixels(texture.xPos, texture.yPos, texture.width, texture.height));
            return newTexture;
        }

        public TextureCollection GetTextureCollection(string textureName)
        {
            if (textures.Keys.Count == 0)
                LoadTextureIndex();

            TextureCollection collection;
            textures.TryGetValue(textureName, out collection);
            return collection;
        }

    }
}
