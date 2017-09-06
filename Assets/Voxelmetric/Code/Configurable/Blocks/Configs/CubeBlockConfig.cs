using Newtonsoft.Json;
using System.Collections;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Load_Resources.Textures;

[System.Obsolete("Use 'Dev_CubeBlockConfig' instead.")]
public class CubeBlockConfig : BlockConfig
{
    private TextureCollection[] m_Textures;
    public TextureCollection[] Textures { get { return m_Textures; } set { m_Textures = value; } }

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        m_Textures = new TextureCollection[6];
        Newtonsoft.Json.Linq.JArray textureNames = (Newtonsoft.Json.Linq.JArray)JsonConvert.DeserializeObject(config["textures"].ToString());

        for (int i = 0; i < 6; i++)
            m_Textures[i] = world.TextureProvider.GetTextureCollection(textureNames[i].ToObject<string>());

        return true;
    }
}
