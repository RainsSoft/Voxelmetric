using System.Collections;

namespace Voxelmetric.Code.Load_Resources
{
    [System.Obsolete("Use 'Dev_LayerConfig' instead.")]
    public struct LayerConfig
    {
        private string m_Name;
        public string Name { get { return m_Name; } set { m_Name = value; } }

        // This is used to sort the layers, low numbers are applied first
        // does not need to be consecutive so use numbers like 100 so that
        // layer you can add layers in between if you have to
        private int m_Index;
        public int Index { get { return m_Index; } set { m_Index = value; } }
        private string m_LayerType;
        public string LayerType { get { return m_LayerType; } set { m_LayerType = value; } }
        private string m_Structure;
        public string Structure { get { return m_Structure; } set { m_Structure = value; } }
        private Hashtable m_Properties;
        public Hashtable Properties { get { return m_Properties; } set { m_Properties = value; } }

        public static bool IsStructure(string structure)
        {
            return !string.IsNullOrEmpty(structure);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
