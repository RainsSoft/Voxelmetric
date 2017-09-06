using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Geometry;

namespace Voxelmetric.Code.Configurable
{
    [CreateAssetMenu(fileName = "New Custom Mesh Block Config", menuName = "Voxelmetric/Blocks/Custom Mesh Block")]
    public class Dev_CustomMeshBlockConfig : Dev_BlockConfig
    {
        [SerializeField]
        private Mesh m_Mesh;
        public Mesh Mesh { get { return m_Mesh; } set { m_Mesh = value; } }
        [SerializeField]
        private Vector3 m_MeshOffset;
        public Vector3 MeshOffset { get { return m_MeshOffset; } set { m_MeshOffset = value; } }
        [SerializeField]
        private Texture2D m_MeshTexture;
        public Texture2D MeshTexture { get { return m_MeshTexture; } set { m_MeshTexture = value; } }

        private int[] m_Triangles;
        public int[] Triangles { get { return m_Triangles; } }
        private VertexData[] m_Vertices;
        public VertexData[] Vertices { get { return m_Vertices; } }

        public override bool OnSetUp(World world)
        {
            Vector3 meshOffset;
            meshOffset.x = Env.BLOCK_SIZE_HALF + MeshOffset.x;
            meshOffset.y = Env.BLOCK_SIZE_HALF + MeshOffset.y;
            meshOffset.z = Env.BLOCK_SIZE_HALF + MeshOffset.z;

            SetUpMesh(meshOffset, out m_Triangles, out m_Vertices);
            return true;
        }

        protected virtual void SetUpMesh(Vector3 positionOffset, out int[] triangles, out VertexData[] vertices)
        {
            int vertexCount = Mesh.vertices.Length;
            int triangleCount = Mesh.triangles.Length;

            triangles = new int[triangleCount];
            vertices = new VertexData[vertexCount];

            int ti = 0, vi = 0;

            for (int i = 0; i < Mesh.vertices.Length; i++, vi++)
            {
                vertices[vi] = new VertexData()
                {
                    vertex = Mesh.vertices[i] + positionOffset,
                    uv = Mesh.uv.Length != 0 ? Mesh.uv[i] : new Vector2(),
                    // Coloring of blocks is not yet implemented so just pass in full brightness
                    color = new Color32(255, 255, 255, 255)
                };
            }

            for (int i = 0; i < Mesh.triangles.Length; i++, ti++)
                triangles[ti] = Mesh.triangles[i];
        }
    }
}
