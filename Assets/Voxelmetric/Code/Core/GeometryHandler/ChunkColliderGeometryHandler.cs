using UnityEngine;
using Voxelmetric.Code.Geometry.GeometryHandler;

namespace Voxelmetric.Code.Core.GeometryHandler
{
    public class ChunkColliderGeometryHandler : AColliderGeometryHandler
    {
        private const string POOL_ENTRY_NAME = "Collidable";
        private readonly Chunk chunk;

        public ChunkColliderGeometryHandler(Chunk chunk, PhysicMaterial[] materials) : base(POOL_ENTRY_NAME, materials)
        {
            this.chunk = chunk;
        }

        /// <summary> Updates the chunk based on its contents </summary>
        public override void Build()
        {
            Globals.CubeMeshColliderBuilder.SideMask = Features.DONT_RENDER_WORLD_EDGE_MASKS;
            Globals.CubeMeshColliderBuilder.Build(chunk);
        }

        public override void Commit()
        {
            Batcher.Commit(
                chunk.World.transform.rotation * chunk.Pos + chunk.World.transform.position,
                chunk.World.transform.rotation
#if DEBUG
                , chunk.Pos + "C"
#endif
                );
        }
    }
}
