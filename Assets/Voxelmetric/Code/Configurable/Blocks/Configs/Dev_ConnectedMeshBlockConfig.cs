using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;

namespace Voxelmetric.Code.Configurable
{
    [CreateAssetMenu(fileName = "New Connected Mesh Block Config", menuName = "Voxelmetric/Blocks/Connected Mesh Block")]
    public class Dev_ConnectedMeshBlockConfig : Dev_CustomMeshBlockConfig
    {
        private int[][] m_DirectionalTris = new int[6][];
        public int[][] DirectionalTris { get { return m_DirectionalTris; } }
        private VertexData[][] m_DirectionalVerts = new VertexData[6][];
        public VertexData[][] DirectionalVerts { get { return m_DirectionalVerts; } set { m_DirectionalVerts = value; } }

        [SerializeField]
        private int[] m_ConnectsToTypes;
        public int[] ConnectsToTypes { get { return m_ConnectsToTypes; } set { m_ConnectsToTypes = value; } }
        [SerializeField]
        private string[] m_ConnectsToNames;
        public string[] ConnectsToNames { get { return m_ConnectsToNames; } set { m_ConnectsToNames = value; } }
        [SerializeField]
        private bool m_ConnectsToSolid = true;
        public bool ConnectsToSolid { get { return m_ConnectsToSolid; } set { m_ConnectsToSolid = value; } }

        public override bool OnSetUp(World world)
        {
            for (int dir = 0; dir < 6; dir++)
            {
                Direction direction = DirectionUtils.Get(dir);

                Vector3 offset;
                offset.x = (int)direction + MeshOffset.x;
                offset.y = (int)direction + MeshOffset.y;
                offset.z = (int)direction + MeshOffset.z;

                int[] newTris;
                VertexData[] newVerts;

                SetUpMesh(offset, out newTris, out newVerts);

                DirectionalTris[dir] = newTris;
                DirectionalVerts[dir] = newVerts;
            }

            return true;
        }
    }
}
