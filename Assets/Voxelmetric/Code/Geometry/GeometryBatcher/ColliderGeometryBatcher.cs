using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code.Builders;
using Voxelmetric.Code.Common.MemoryPooling;

namespace Voxelmetric.Code.Geometry.GeometryBatcher
{
    public class ColliderGeometryBatcher : IGeometryBatcher
    {
        private readonly string prefabName;
        //! Materials our meshes are to use
        private readonly PhysicMaterial[] physicMaterials;
        //! A list of buffers for each material
        private readonly List<ColliderGeometryBuffer>[] buffers;
        //! GameObjects used to hold our geometry
        private readonly List<GameObject> objects;
        //! A list of renderer used to render our geometry
        private readonly List<Collider> colliders;

        private bool m_enabled;

        public bool Enabled
        {
            get { return m_enabled; }
            set
            {
                for (int i = 0; i < colliders.Count; i++)
                {
                    Collider collider = colliders[i];
                    collider.enabled = value;
                }
                m_enabled = value && colliders.Count > 0;
            }
        }

        public ColliderGeometryBatcher(string prefabName, PhysicMaterial[] materials)
        {
            this.prefabName = prefabName;
            this.physicMaterials = materials;

            int buffersLen = (materials == null || materials.Length < 1) ? 1 : materials.Length;
            buffers = new List<ColliderGeometryBuffer>[buffersLen];
            for (int i = 0; i < buffers.Length; i++)
            {
                /* TODO: Let's be optimistic and allocate enough room for just one buffer. It's going to suffice
                 * in >99% of cases. However, this prediction should maybe be based on chunk size rather then
                 * optimism. The bigger the chunk the more likely we're going to need to create more meshes to
                 * hold its geometry because of Unity's 65k-vertices limit per mesh. For chunks up to 32^3 big
                 * this should not be an issue, though.
                 */
                buffers[i] = new List<ColliderGeometryBuffer>(1)
                {
                    // Default render buffer
                    new ColliderGeometryBuffer()
                };
            }

            objects = new List<GameObject>();
            colliders = new List<Collider>();

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
                        geometryBuffer[j] = new ColliderGeometryBuffer();
                }
            }

            ReleaseOldData();
            m_enabled = false;
        }

        /// <summary>
        ///     Clear all draw calls
        /// </summary>
        public void Clear()
        {
            foreach (var holder in buffers)
            {
                for (int i = 0; i < holder.Count; i++)
                    holder[i].Clear();
            }

            ReleaseOldData();
            m_enabled = false;
        }

        /// <summary>
        ///     Addds one face to our render buffer
        /// </summary>
        /// <param name="vertexData"> An array of 4 vertices forming the face</param>
        /// <param name="backFace">If false, vertices are added clock-wise</param>
        /// <param name="materialID">ID of material to use when building the mesh</param>
        public void AddFace(Vector3[] vertexData, bool backFace, int materialID)
        {
            Assert.IsTrue(vertexData.Length == 4);

            List<ColliderGeometryBuffer> holder = buffers[materialID];
            ColliderGeometryBuffer buffer = holder[holder.Count - 1];

            // If there are too many vertices we need to create a new separate buffer for them
            if (buffer.vertices.Count + 4 > 65000)
            {
                buffer = new ColliderGeometryBuffer();
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

                for (int i = 0; i < holder.Count; i++)
                {
                    ColliderGeometryBuffer buffer = holder[i];

                    // No data means there's no mesh to build
                    if (buffer.IsEmpty())
                        continue;

                    // Create a game object for collider. Unfortunatelly, we can't use object pooling
                    // here for otherwise, unity would have to rebake because of change in object position
                    // and that is very time consuming.
                    GameObject prefab = GameObjectProvider.GetPool(prefabName).Prefab;
                    GameObject go = Object.Instantiate(prefab);
                    go.transform.parent = GameObjectProvider.Instance.ProviderGameObject.transform;

                    {
#if DEBUG
                        go.name = string.Format(debugName, "_", i.ToString());
#endif

                        Mesh mesh = Globals.MemPools.meshPool.Pop();
                        Assert.IsTrue(mesh.vertices.Length <= 0);
                        UnityMeshBuilder.BuildColliderMesh(mesh, buffer);

                        MeshCollider collider = go.GetComponent<MeshCollider>();
                        collider.sharedMesh = null;
                        collider.sharedMesh = mesh;
                        collider.transform.position = position;
                        collider.transform.rotation = rotation;
                        collider.sharedMaterial = (physicMaterials == null || physicMaterials.Length < 1) ? null : physicMaterials[j];

                        objects.Add(go);
                        colliders.Add(collider);
                    }

                    buffer.Clear();
                }
            }
        }

        private void ReleaseOldData()
        {
            Assert.IsTrue(objects.Count == colliders.Count);
            for (int i = 0; i < objects.Count; i++)
            {
                var go = objects[i];
                // If the component does not exist it means nothing else has been added as well
                if (go == null)
                    continue;

#if DEBUG
                go.name = prefabName;
#endif

                MeshCollider collider = go.GetComponent<MeshCollider>();
                collider.sharedMesh.Clear(false);
                Globals.MemPools.meshPool.Push(collider.sharedMesh);
                collider.sharedMesh = null;
                collider.sharedMaterial = null;

                Object.DestroyImmediate(go);
            }

            if (objects.Count > 0)
                objects.Clear();
            if (colliders.Count > 0)
                colliders.Clear();
        }
    }
}
