using System.Collections;
using System.Globalization;
using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Load_Resources.Textures;

public class ConnectedMeshBlockConfig : CustomMeshBlockConfig
{
    public readonly int[][] directionalTris = new int[6][];
    public readonly VertexData[][] directionalVerts = new VertexData[6][];
    public readonly TextureCollection[] directionalTextures = new TextureCollection[6];

    private int[] m_ConnectsToTypes;
    public int[] ConnectsToTypes { get { return m_ConnectsToTypes; } set { m_ConnectsToTypes = value; } }
    private string[] m_ConnectsToNames;
    public string[] ConnectsToNames { get { return m_ConnectsToNames; } set { m_ConnectsToNames = value; } }
    private bool m_ConnectsToSolid;
    public bool ConnectsToSolid { get { return m_ConnectsToSolid; } set { m_ConnectsToSolid = value; } }

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        m_ConnectsToNames = _GetPropertyFromConfig(config, "connectsToNames", "").Replace(" ", "").Split(',');
        m_ConnectsToSolid = _GetPropertyFromConfig(config, "connectsToSolid", true);

        for (int dir = 0; dir < 6; dir++)
        {
            Direction direction = DirectionUtils.Get(dir);
            if (_GetPropertyFromConfig(config, direction + "FileLocation", "") == "")
                continue;

            directionalTextures[dir] = world.TextureProvider.GetTextureCollection(_GetPropertyFromConfig(config, direction + "Texture", ""));

            Vector3 offset;
            offset.x = float.Parse(_GetPropertyFromConfig(config, direction + "XOffset", "0"), CultureInfo.InvariantCulture);
            offset.y = float.Parse(_GetPropertyFromConfig(config, direction + "YOffset", "0"), CultureInfo.InvariantCulture);
            offset.z = float.Parse(_GetPropertyFromConfig(config, direction + "ZOffset", "0"), CultureInfo.InvariantCulture);

            int[] newTris;
            VertexData[] newVerts;
            string meshLocation = world.Config.meshFolder + "/" + _GetPropertyFromConfig(config, direction + "FileLocation", "");

            SetUpMesh(meshLocation, offset, out newTris, out newVerts);

            directionalTris[dir] = newTris;
            directionalVerts[dir] = newVerts;
        }

        return true;
    }
}
