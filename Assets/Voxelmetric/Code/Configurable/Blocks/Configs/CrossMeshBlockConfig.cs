using System.Collections;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Load_Resources.Textures;

[System.Obsolete("Use 'Dev_CrossMeshBlockConfig' instead.")]
public class CrossMeshBlockConfig : BlockConfig
{
    private TextureCollection m_Texture;
    public TextureCollection Texture { get { return m_Texture; } set { m_Texture = value; } }

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        m_Texture = world.TextureProvider.GetTextureCollection(_GetPropertyFromConfig(config, "texture", ""));
        Solid = _GetPropertyFromConfig(config, "solid", false);

        return true;
    }
}
