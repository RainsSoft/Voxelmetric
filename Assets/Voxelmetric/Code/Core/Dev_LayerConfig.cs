using System.Collections;
using UnityEngine;

namespace Voxelmetric.Code.Configurable
{
    [CreateAssetMenu(fileName = "New Layer Config", menuName = "Voxelmetric/Layers/Standard Layer")]
    public class Dev_LayerConfig : ScriptableObject
    {
        public enum LayerTypes { AbsoluteLayer };

        [SerializeField]
        private string m_LayerName;
        public string LayerName { get { return m_LayerName; } set { m_LayerName = value; } }
        [SerializeField]
        private int m_Index;
        public int Index { get { return m_Index; } set { m_Index = value; } }
        [SerializeField]
        private LayerTypes m_LayerType;
        public LayerTypes LayerType { get { return m_LayerType; } set { m_LayerType = value; } }
        [SerializeField]
        private Hashtable m_Properties;
        public Hashtable Properties { get { return m_Properties; } set { m_Properties = value; } }
    }
}
