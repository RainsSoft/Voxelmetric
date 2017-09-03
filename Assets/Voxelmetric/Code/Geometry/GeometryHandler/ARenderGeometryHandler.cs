using UnityEngine;
using Voxelmetric.Code.Geometry.GeometryBatcher;

namespace Voxelmetric.Code.Geometry.GeometryHandler
{
    public abstract class ARenderGeometryHandler : IGeometryHandler
    {
        public RenderGeometryBatcher Batcher { get; private set; }

        protected ARenderGeometryHandler(string prefabName, Material[] materials)
        {
            Batcher = new RenderGeometryBatcher(prefabName, materials);
        }

        public void Reset()
        {
            Batcher.Reset();
        }

        /// <summary> Updates the chunk based on its contents </summary>
        public abstract void Build();

        public abstract void Commit();
    }
}
