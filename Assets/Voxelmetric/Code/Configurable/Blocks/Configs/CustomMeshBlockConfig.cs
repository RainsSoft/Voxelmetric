using System.Collections;
using System.Globalization;
using UnityEngine;
using Voxelmetric.Code;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Load_Resources.Textures;

public class CustomMeshBlockConfig : BlockConfig
{
    private TextureCollection[] m_Textures;
    public TextureCollection[] Textures { get { return m_Textures; } set { m_Textures = value; } }

    public int[] Tris { get { return m_Triangles; } }
    public VertexData[] Verts { get { return m_Vertices; } }

    private TextureCollection m_Texture;
    public TextureCollection Texture { get { return m_Texture; } set { m_Texture = value; } }

    private int[] m_Triangles;
    private VertexData[] m_Vertices;

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        Solid = _GetPropertyFromConfig(config, "solid", false);
        m_Texture = world.TextureProvider.GetTextureCollection(_GetPropertyFromConfig(config, "texture", ""));

        Vector3 meshOffset;
        meshOffset.x = Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshXOffset", "0"), CultureInfo.InvariantCulture);
        meshOffset.y = Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshYOffset", "0"), CultureInfo.InvariantCulture);
        meshOffset.z = Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshZOffset", "0"), CultureInfo.InvariantCulture);

        SetUpMesh(world.Config.meshFolder + "/" + _GetPropertyFromConfig(config, "meshFileLocation", ""), meshOffset, out m_Triangles, out m_Vertices);
        return true;
    }

    protected static void SetUpMesh(string meshLocation, Vector3 positionOffset, out int[] trisOut, out VertexData[] vertsOut)
    {
        GameObject meshGO = (GameObject)Resources.Load(meshLocation);

        int vertexCnt = 0;
        int triangleCnt = 0;

        for (int GOIndex = 0; GOIndex < meshGO.transform.childCount; GOIndex++)
        {
            Mesh mesh = meshGO.transform.GetChild(GOIndex).GetComponent<MeshFilter>().sharedMesh;

            vertexCnt += mesh.vertices.Length;
            triangleCnt += mesh.triangles.Length;
        }

        trisOut = new int[triangleCnt];
        vertsOut = new VertexData[vertexCnt];

        int ti = 0, vi = 0;

        for (int GOIndex = 0; GOIndex < meshGO.transform.childCount; GOIndex++)
        {
            Mesh mesh = meshGO.transform.GetChild(GOIndex).GetComponent<MeshFilter>().sharedMesh;

            for (int i = 0; i < mesh.vertices.Length; i++, vi++)
            {
                vertsOut[vi] = new VertexData
                {
                    vertex = mesh.vertices[i] + positionOffset,
                    uv = mesh.uv.Length != 0 ? mesh.uv[i] : new Vector2(),
                    //Coloring of blocks is not yet implemented so just pass in full brightness
                    color = new Color32(255, 255, 255, 255)
                };
            }

            for (int i = 0; i < mesh.triangles.Length; i++, ti++)
                trisOut[ti] = mesh.triangles[i];
        }
    }
}
