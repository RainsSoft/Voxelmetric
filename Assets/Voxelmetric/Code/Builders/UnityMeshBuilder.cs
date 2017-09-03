using UnityEngine;
using Voxelmetric.Code.Geometry;

namespace Voxelmetric.Code.Builders
{
    public static class UnityMeshBuilder
    {
        /// <summary>
        /// Build a Unity3D mesh from provided data
        /// </summary>
        public static void BuildRenderMesh(Mesh mesh, RenderGeometryBuffer buffer, bool useColors, bool useTextures, bool useTangents)
        {
            int size = buffer.vertices.Count;
            var pools = Globals.MemPools;

            // Avoid allocations by retrieving buffers from the pool
            Vector3[] vertices = pools.vector3ArrayPool.Pop(size);
            Vector2[] uvs = useTextures ? pools.vector2ArrayPool.Pop(size) : null;
            Color32[] colors = useColors ? pools.color32ArrayPool.Pop(size) : null;
            Vector4[] tangents = useTangents ? pools.vector4ArrayPool.Pop(size) : null;

            // Fill buffers with data.
            // Due to the way the memory pools work we might have received more
            // data than necessary. This little overhead is well worth it, though.
            // Fill unused data with "zeroes"
            // TODO: Make it so that vertex count is known ahead of time
            for (int i = 0; i < size; i++)
                vertices[i] = buffer.vertices[i].vertex;
            for (int i = size; i < vertices.Length; i++)
                vertices[i] = Vector3.zero;

            if (useTextures)
            {
                for (int i = 0; i < size; i++)
                    uvs[i] = buffer.vertices[i].uv;
                for (int i = size; i < uvs.Length; i++)
                    uvs[i] = Vector2.zero;
            }

            if (useColors)
            {
                for (int i = 0; i < size; i++)
                    colors[i] = buffer.vertices[i].color;
                for (int i = size; i < colors.Length; i++)
                    colors[i] = new Color32();
            }

            /*if (useTangents)
            {
                for (int i = 0; i<size; i++)
                    tangents[i] = buffer.Vertices[i].Tangent;
                for (int i = size; i<tangents.Length; i++)
                    tangents[i] = Vector4.zero;
            }*/

            // Prepare mesh
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.uv2 = null;
            mesh.uv3 = null;
            mesh.uv4 = null;
            mesh.colors32 = colors;
            mesh.normals = null;
            mesh.tangents = tangents;
            mesh.SetTriangles(buffer.triangles, 0);
            mesh.RecalculateNormals();

            // Return memory back to pool
            pools.vector3ArrayPool.Push(vertices);
            if (useTextures)
                pools.vector2ArrayPool.Push(uvs);
            if (useColors)
                pools.color32ArrayPool.Push(colors);
            if (useTangents)
                pools.vector4ArrayPool.Push(tangents);
        }

        /// <summary>
        ///     Build a Unity3D mesh from provided data
        /// </summary>
        public static void BuildColliderMesh(Mesh mesh, ColliderGeometryBuffer buffer)
        {
            int size = buffer.vertices.Count;
            var pools = Globals.MemPools;

            // Avoid allocations by retrieving buffers from the pool
            Vector3[] vertices = pools.vector3ArrayPool.Pop(size);

            // Fill buffers with data.
            // Due to the way the memory pools work we might have received more
            // data than necessary. This little overhead is well worth it, though.
            // Fill unused data with "zeroes"
            // TODO: Make it so that vertex count is known ahead of time
            for (int i = 0; i < size; i++)
                vertices[i] = buffer.vertices[i];
            for (int i = size; i < vertices.Length; i++)
                vertices[i] = Vector3.zero;

            // Prepare mesh
            mesh.vertices = vertices;
            mesh.uv = null;
            mesh.uv2 = null;
            mesh.uv3 = null;
            mesh.uv4 = null;
            mesh.colors32 = null;
            mesh.normals = null;
            mesh.tangents = null;
            mesh.SetTriangles(buffer.triangles, 0);
            mesh.RecalculateNormals();

            // Return memory back to pool
            pools.vector3ArrayPool.Push(vertices);
        }
    }
}
