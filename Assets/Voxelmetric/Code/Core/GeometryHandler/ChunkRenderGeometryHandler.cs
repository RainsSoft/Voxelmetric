using UnityEngine;
using Voxelmetric.Code.Geometry.GeometryHandler;

namespace Voxelmetric.Code.Core.GeometryHandler
{
    public class ChunkRenderGeometryHandler : ARenderGeometryHandler
    {
        private const string POOL_ENTRY_NAME = "Renderable";
        private readonly Chunk chunk;

        public ChunkRenderGeometryHandler(Chunk chunk, Material[] materials) : base(POOL_ENTRY_NAME, materials)
        {
            this.chunk = chunk;
        }

        /// <summary> Updates the chunk based on its contents </summary>
        public override void Build()
        {
            Globals.CubeMeshBuilder.SideMask = Features.DontRenderWorldEdgesMask;
            Globals.CubeMeshBuilder.Build(chunk);
        }

        public override void Commit()
        {
            Batcher.Commit(
                chunk.World.transform.rotation * chunk.Pos + chunk.World.transform.position,
                chunk.World.transform.rotation
#if DEBUG
                , chunk.Pos.ToString()
#endif
                );
        }
    }
}
