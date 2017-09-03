using System;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.Memory;

namespace Voxelmetric.Code.Common.MemoryPooling
{
    [AddComponentMenu("VoxelMetric/Singleton/GameObjectProvider")]
    public sealed class GameObjectProvider : MonoSingleton<GameObjectProvider>
    {
        private GameObject m_Go;
        [SerializeField]
        private ObjectPoolEntry[] m_ObjectPools = new ObjectPoolEntry[0];
        public ObjectPoolEntry[] ObjectPools { get { return m_ObjectPools; } set { m_ObjectPools = value; } }

        private readonly StringBuilder stringBuilder = new StringBuilder();

        public GameObject ProviderGameObject { get { return m_Go; } }

        // Called after the singleton instance is created
        private void Awake()
        {
            m_Go = new GameObject("GameObjects");
            m_Go.transform.parent = gameObject.transform;

            // Iterate pool entries and create a pool of prefabs for each of them
            for (int i = 0; i < Instance.ObjectPools.Length; i++)
            {
                if (Instance.ObjectPools[i].Prefab == null)
                {
                    Debug.LogError("No prefab specified in one of the object pool's entries");
                    continue;
                }

                Instance.ObjectPools[i].Init(m_Go, Instance.ObjectPools[i].Prefab);
            }
        }

        // Returns a pool of a given name if it exists
        public static ObjectPoolEntry GetPool(string poolName)
        {
            for (int i = 0; i < Instance.ObjectPools.Length; i++)
            {
                if (Instance.ObjectPools[i].Name == poolName)
                    return Instance.ObjectPools[i];
            }
            return null;
        }

        public static void PushObject(string poolName, GameObject go)
        {
            if (go == null)
                throw new ArgumentNullException(string.Format("Trying to pool a null game object in pool {0}", poolName));

            ObjectPoolEntry pool = GetPool(poolName);
            if (pool == null)
                throw new InvalidOperationException(string.Format("Object pool {0} does not exist", poolName));

            pool.Push(go);
        }

        public static GameObject PopObject(string poolName)
        {
            ObjectPoolEntry pool = GetPool(poolName);
            if (pool == null)
                throw new InvalidOperationException(string.Format("Object pool {0} does not exist", poolName));

            return pool.Pop();
        }

        public override string ToString()
        {
            stringBuilder.Length = 0;

            stringBuilder.Append("ObjectPools ");
            for (int i = 0; i < ObjectPools.Length; i++)
                stringBuilder.ConcatFormat("{0}", ObjectPools[i].ToString());
            return stringBuilder.ToString();
        }

        [Serializable]
        public class ObjectPoolEntry
        {
            [SerializeField]
            private string m_Name;
            public string Name { get { return m_Name; } set { m_Name = value; } }
            [SerializeField]
            private GameObject m_Prefab;
            public GameObject Prefab { get { return m_Prefab; } set { m_Prefab = value; } }
            [SerializeField]
            private int m_InitialSize = 128;
            public int InitialSize { get { return m_InitialSize; } set { m_InitialSize = value; } }

            private ObjectPool<GameObject> m_Cache;
            public ObjectPool<GameObject> Cache { get { return m_Cache; } set { m_Cache = value; } }

            private GameObject m_ParentGo;

            public ObjectPoolEntry()
            {
                //Name = "";
                //InitialSize = 0;
                m_ParentGo = null;
                Prefab = null;
                m_Cache = null;
            }

            public void Init(GameObject parentGo, GameObject prefab)
            {
                m_ParentGo = parentGo;
                Prefab = prefab;

                m_Cache = new ObjectPool<GameObject>(arg =>
                    {
                        GameObject newGO = Instantiate(Prefab);
                        newGO.name = Prefab.name;
                        newGO.SetActive(false);
                        newGO.transform.parent = m_ParentGo.transform; // Make this object a parent of the pooled object
                        return newGO;
                    },
                    InitialSize, false
                );
            }

            public void Push(GameObject go)
            {
                // Deactive object, reset its' transform and physics data
                go.SetActive(false);

                Rigidbody rbody = go.GetComponent<Rigidbody>();
                if (rbody != null)
                    rbody.velocity = Vector3.zero;

                // Place a pointer to our object to the back of our cache list
                m_Cache.Push(go);
            }

            public GameObject Pop()
            {
                GameObject go = m_Cache.Pop();

                // Reset transform and active it
                //go.transform.parent = null;
                Assert.IsTrue(!go.activeSelf, "Popped an active gameObject!");
                go.SetActive(true);

                return go;
            }

            public override string ToString()
            {
                return string.Format("{0}={1}", Name, m_Cache);
            }
        }
    }
}
