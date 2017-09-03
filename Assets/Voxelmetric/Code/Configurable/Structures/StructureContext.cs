using UnityEngine;
using Voxelmetric.Code.Core;

namespace Voxelmetric.Code.Configurable.Structures
{
    public abstract class StructureContext
    {
        //! A chunk this structure belongs to
        public Vector3Int m_ChunkPos;
        //! ID of associate structure
        public readonly int id;

        protected StructureContext(int id, ref Vector3Int chunkPos)
        {
            m_ChunkPos = chunkPos;
            this.id = id;
        }

        public abstract void Apply(Chunk chunk);
    }
}
