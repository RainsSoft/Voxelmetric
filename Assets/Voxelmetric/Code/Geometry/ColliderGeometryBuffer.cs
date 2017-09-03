using System.Collections.Generic;
using UnityEngine;

namespace Voxelmetric.Code.Geometry
{
    /// <summary>
    ///     A simple intermediate container for mesh data
    /// </summary>
    public class ColliderGeometryBuffer
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<int> triangles = new List<int>();

        /// <summary>
        ///     Clear the render buffer
        /// </summary>
        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
        }

        public bool IsEmpty()
        {
            return vertices.Count <= 0;
        }

        public bool WasUsed()
        {
            return vertices.Capacity > 0;
        }

        /// <summary>
        ///     Adds triangle indices for a quad
        /// </summary>
        public void AddIndices(int offset, bool backFace)
        {
            // 0--1
            // |\ |
            // | \|
            // 3--2
            if (backFace)
            {
                triangles.Add(offset - 4); // 0
                triangles.Add(offset - 1); // 3
                triangles.Add(offset - 2); // 2

                triangles.Add(offset - 2); // 2
                triangles.Add(offset - 3); // 1
                triangles.Add(offset - 4); // 0
            }
            else
            {
                triangles.Add(offset - 4); // 0
                triangles.Add(offset - 3); // 1
                triangles.Add(offset - 2); // 2

                triangles.Add(offset - 2); // 2
                triangles.Add(offset - 1); // 3
                triangles.Add(offset - 4); // 0
            }
        }

        public void AddIndex(int offset)
        {
            triangles.Add(offset);
        }

        /// <summary>
        ///     Adds the vertices to the render buffer.
        /// </summary>
        public void AddVertices(Vector3[] vertices)
        {
            this.vertices.AddRange(vertices);
        }

        public void AddVertex(ref Vector3 vertex)
        {
            vertices.Add(vertex);
        }
    }
}
