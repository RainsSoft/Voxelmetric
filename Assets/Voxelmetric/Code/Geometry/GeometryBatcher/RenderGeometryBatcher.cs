using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code.Builders;
using Voxelmetric.Code.Common.MemoryPooling;

namespace Voxelmetric.Code.Geometry.GeometryBatcher
{
    public class RenderGeometryBatcher : IGeometryBatcher
    {
        private readonly string prefabName;
        //! Materials our meshes are to use
        private readonly Material[] materials;
        //! A list of buffers for each material
        private readonly List<RenderGeometryBuffer>[] buffers;
        private readonly BufferProperties[] buffersProperties;
        //! GameObjects used to hold our geometry
        private readonly List<GameObject> objects;
        //! A list of renderer used to render our geometry
        private readonly List<Renderer> renderers;

        private bool m_enabled;
        public bool Enabled
        {
            get { return m_enabled; }
            set
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    renderer.enabled = value;
                }
                m_enabled = value;
            }

        }

        public RenderGeometryBatcher(string prefabName, Material[] materials)
        {
            this.prefabName = prefabName;
            this.materials = materials;

            int buffersCount = materials == null || materials.Length < 1 ? 1 : materials.Length;
            buffers = new List<RenderGeometryBuffer>[buffersCount];
            buffersProperties = new BufferProperties[buffersCount];

            for (int i = 0; i < buffers.Length; i++)
            {
                /* TODO: Let's be optimistic and allocate enough room for just one buffer. It's going to suffice
                 * in >99% of cases. However, this prediction should maybe be based on chunk size rather then
                 * optimism. The bigger the chunk the more likely we're going to need to create more meshes to
                 * hold its geometry because of Unity's 65k-vertices limit per mesh. For chunks up to 32^3 big
                 * this should not be an issue, though.
                 */
                buffers[i] = new List<RenderGeometryBuffer>(1)
                {
                    // Default render buffer
                    new RenderGeometryBuffer()
                };
            }

            objects = new List<GameObject>(1);
            renderers = new List<Renderer>(1);

            Clear();
        }

        public void Reset()
        {
            // Buffers need to be reallocated. Otherwise, more and more memory would be consumed by them. This is
            // because internal arrays grow in capacity and we can't simply release their memory by calling Clear().
            // Objects and renderers are fine, because there's usually only 1 of them. In some extreme cases they
            // may grow more but only by 1 or 2 (and only if Env.ChunkPow>5).
            for (int i = 0; i < buffers.Length; i++)
            {
                var geometryBuffer = buffers[i];
                for (int j = 0; j < geometryBuffer.Count; j++)
                {
                    if (geometryBuffer[j].WasUsed())
                        geometryBuffer[j] = new RenderGeometryBuffer();
                }

                buffersProperties[i] = new BufferProperties();
            }

            ReleaseOldData();
            m_enabled = false;
        }

        /// <summary>
        ///     Clear all draw calls
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                var geometryBuffer = buffers[i];
                for (int j = 0; j < geometryBuffer.Count; j++)
                {
                    geometryBuffer[j].Clear();
                }

                buffersProperties[i] = new BufferProperties();
            }

            ReleaseOldData();
            m_enabled = false;
        }

        /// <summary>
        ///     Addds one face to our render buffer
        /// </summary>
        /// <param name="tris">Triangles to be processed</param>
        /// <param name="verts">Vertices to be processed</param>
        /// <param name="texture">Texture coordinates</param>
        /// <param name="offset">Offset to apply to verts</param>
        /// <param name="materialID">ID of material to use when building the mesh</param>
        public void AddMeshData(int[] tris, VertexData[] verts, ref Rect texture, Vector3 offset, int materialID)
        {
            List<RenderGeometryBuffer> holder = buffers[materialID];
            RenderGeometryBuffer buffer = holder[holder.Count - 1];

            int initialVertCount = buffer.vertices.Count;

            for (int i = 0; i < verts.Length; i++)
            {
                // If there are too many vertices we need to create a new separate buffer for them
                if (buffer.vertices.Count + 1 > 65000)
                {
                    buffer = new RenderGeometryBuffer();
                    holder.Add(buffer);
                }

                VertexData v = new VertexData()
                {
                    color = verts[i].color,
                    normal = verts[i].normal,
                    //Tangent = verts[i].Tangent,
                    // Adjust UV coordinates based on provided texture atlas
                    uv = new Vector2(
                        (verts[i].uv.x * texture.width) + texture.x,
                        (verts[i].uv.y * texture.height) + texture.y
                        ),
                    vertex = verts[i].vertex + offset
                };
                buffer.AddVertex(ref v);
            }

            for (int i = 0; i < tris.Length; i++)
                buffer.AddIndex(tris[i] + initialVertCount);
        }

        /// <summary>
        ///     Addds one face to our render buffer
        /// </summary>
        /// <param name="vertexData"> An array of 4 vertices forming the face</param>
        /// <param name="backFace">If false, vertices are added clock-wise</param>
        /// <param name="materialID">ID of material to use when building the mesh</param>
        public void AddFace(VertexData[] vertexData, bool backFace, int materialID)
        {
            Assert.IsTrue(vertexData.Length == 4);

            List<RenderGeometryBuffer> holder = buffers[materialID];
            RenderGeometryBuffer buffer = holder[holder.Count - 1];

            // If there are too many vertices we need to create a new separate buffer for them
            if (buffer.vertices.Count + 4 > 65000)
            {
                buffer = new RenderGeometryBuffer();
                holder.Add(buffer);
            }

            // Add data to the render buffer
            buffer.AddVertices(vertexData);
            buffer.AddIndices(buffer.vertices.Count, backFace);
        }

        /// <summary>
        ///     Finalize the draw calls
        /// </summary>
        public void Commit(Vector3 position, Quaternion rotation
#if DEBUG
            , string debugName = null
#endif
            )
        {
            ReleaseOldData();

            for (int j = 0; j < buffers.Length; j++)
            {
                var holder = buffers[j];
                var props = buffersProperties[j];
                int propsMask = props.GetMask;

                for (int i = 0; i < holder.Count; i++)
                {
                    RenderGeometryBuffer buffer = holder[i];

                    // No data means there's no mesh to build
                    if (buffer.IsEmpty())
                        continue;

                    var go = GameObjectProvider.PopObject(prefabName);
                    Assert.IsTrue(go != null);
                    if (go != null)
                    {
#if DEBUG
                        go.name = string.Format(debugName, "_", i.ToString());
#endif

                        Mesh mesh = Globals.MemPools.meshPool.Pop();
                        Assert.IsTrue(mesh.vertices.Length <= 0);
                        UnityMeshBuilder.BuildRenderMesh(
                            mesh,
                            buffer,
                            BufferProperties.GetColors(propsMask),
                            BufferProperties.GetTextures(propsMask),
                            BufferProperties.GetTangents(propsMask)
                            );

                        MeshFilter filter = go.GetComponent<MeshFilter>();
                        filter.sharedMesh = null;
                        filter.sharedMesh = mesh;
                        filter.transform.position = position;
                        filter.transform.rotation = rotation;

                        Renderer renderer = go.GetComponent<Renderer>();
                        renderer.sharedMaterial = (materials == null || materials.Length < 1) ? null : materials[j];

                        objects.Add(go);
                        renderers.Add(renderer);
                    }

                    buffer.Clear();
                }

                props = new BufferProperties();
            }
        }

        private void ReleaseOldData()
        {
            Assert.IsTrue(objects.Count == renderers.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                // If the component does not exist it means nothing else has been added as well
                if (go == null)
                    continue;

#if DEBUG
                go.name = prefabName;
#endif

                MeshFilter filter = go.GetComponent<MeshFilter>();
                filter.sharedMesh.Clear(false);
                Globals.MemPools.meshPool.Push(filter.sharedMesh);
                filter.sharedMesh = null;

                Renderer renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = null;

                GameObjectProvider.PushObject(prefabName, go);
            }

            objects.Clear();
            renderers.Clear();
        }

        public void UseColors(int materialID)
        {
            int mask = buffersProperties[materialID].GetMask;
            mask = BufferProperties.SetColors(mask);
            buffersProperties[materialID] = new BufferProperties(mask);
        }

        public void UseTextures(int materialID)
        {
            int mask = buffersProperties[materialID].GetMask;
            mask = BufferProperties.SetTextures(mask);
            buffersProperties[materialID] = new BufferProperties(mask);
        }

        public void UseTangents(int materialID)
        {
            int mask = buffersProperties[materialID].GetMask;
            mask = BufferProperties.SetTangents(mask);
            buffersProperties[materialID] = new BufferProperties(mask);
        }
    }
}
