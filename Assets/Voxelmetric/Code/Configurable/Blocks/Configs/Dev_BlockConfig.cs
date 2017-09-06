using System.Collections;
using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources.Blocks;
using Voxelmetric.Code.Load_Resources.Textures;

namespace Voxelmetric.Code.Configurable
{
    [System.Serializable]
    public struct BlockTextures
    {
        [SerializeField]
        private Texture2D m_Top;
        public Texture2D Top { get { return m_Top; } set { m_Top = value; } }
        [SerializeField]
        private Texture2D m_Bottom;
        public Texture2D Bottom { get { return m_Bottom; } set { m_Bottom = value; } }
        [SerializeField]
        private Texture2D m_North;
        public Texture2D North { get { return m_North; } set { m_North = value; } }
        [SerializeField]
        private Texture2D m_Back;
        public Texture2D Back { get { return m_Back; } set { m_Back = value; } }
        [SerializeField]
        private Texture2D m_East;
        public Texture2D East { get { return m_East; } set { m_East = value; } }
        [SerializeField]
        private Texture2D m_West;
        public Texture2D West { get { return m_West; } set { m_West = value; } }

        public Texture2D GetTextureFromDirection(int direction)
        {
            return GetTextureFromDirection((Direction)direction);
        }

        public Texture2D GetTextureFromDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.up:
                    return Top;
                case Direction.down:
                    return Bottom;
                case Direction.south:
                    return Back;
                case Direction.north:
                    return North;
                case Direction.east:
                    return East;
                case Direction.west:
                    return West;
                default:
                    return null;
            }
        }
    }

    [System.Serializable]
    public class BlockColors
    {
        [SerializeField]
        private Color m_TopColor = Color.white;
        public Color TopColor { get { return m_TopColor; } set { m_TopColor = value; } }
        [SerializeField]
        private Color m_BottomColor = Color.white;
        public Color BottomColor { get { return m_BottomColor; } set { m_BottomColor = value; } }
        [SerializeField]
        private Color m_NorthColor = Color.white;
        public Color NorthColor { get { return m_NorthColor; } set { m_NorthColor = value; } }
        [SerializeField]
        private Color m_BackColor = Color.white;
        public Color BackColor { get { return m_BackColor; } set { m_BackColor = value; } }
        [SerializeField]
        private Color m_EastColor = Color.white;
        public Color EastColor { get { return m_EastColor; } set { m_EastColor = value; } }
        [SerializeField]
        private Color m_WestColor = Color.white;
        public Color WestColor { get { return m_WestColor; } set { m_WestColor = value; } }

        public Color GetColorFromDirection(int direction)
        {
            return GetColorFromDirection((Direction)direction);
        }

        public Color GetColorFromDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.up:
                    return TopColor;
                case Direction.down:
                    return BottomColor;
                case Direction.south:
                    return BackColor;
                case Direction.north:
                    return NorthColor;
                case Direction.east:
                    return EastColor;
                case Direction.west:
                    return WestColor;
                default:
                    return Color.black;
            }
        }
    }

    [CreateAssetMenu(fileName = "New Block Config", menuName = "Voxelmetric/Blocks/Standard Block")]
    public class Dev_BlockConfig : ScriptableObject
    {
        public enum BlockTypeEnum { Textured, Colored };

        [SerializeField]
        private string m_BlockName;
        public string BlockName { get { return m_BlockName; } set { m_BlockName = value; } }
        [SerializeField]
        private ushort m_Type = 1;
        public ushort Type { get { return m_Type; } set { m_Type = value; } }
        [SerializeField]
        private bool m_AutoAssignType = true;
        public bool AutoAssignType { get { return m_AutoAssignType; } set { m_AutoAssignType = value; } }
        [SerializeField]
        private BlockTypeEnum m_BlockType = BlockTypeEnum.Textured;
        public BlockTypeEnum BlockType { get { return m_BlockType; } set { m_BlockType = value; } }
        [SerializeField]
        private bool m_Solid = true;
        public bool Solid { get { return m_Solid; } set { m_Solid = value; } }
        [SerializeField]
        private bool m_Transparent = false;
        public bool Transparent { get { return m_Transparent; } set { m_Transparent = value; } }
        [SerializeField]
        private bool m_RaycastHit;
        public bool RaycastHit { get { return m_RaycastHit; } set { m_RaycastHit = value; } }
        [SerializeField]
        private bool m_RaycastHitOnRemoval;
        public bool RaycastHitOnRemoval { get { return m_RaycastHitOnRemoval; } set { m_RaycastHitOnRemoval = value; } }
        [SerializeField]
        private int m_RenderMaterialID;
        public int RenderMaterialID { get { return m_RenderMaterialID; } set { m_RenderMaterialID = value; } }
        [SerializeField]
        private int m_PhysicMaterialID;
        public int PhysicMaterialID { get { return m_PhysicMaterialID; } set { m_PhysicMaterialID = value; } }
        [SerializeField]
        private BlockTextures m_Textures;
        public BlockTextures Textures { get { return m_Textures; } set { m_Textures = value; } }
        [SerializeField]
        private BlockColors m_Colors;
        public BlockColors Colors { get { return m_Colors; } set { m_Colors = value; } }

        [Space]

        [SerializeField]
        private Material m_Material;
        public Material Material { get { return m_Material; } set { m_Material = value; } }

        private TextureCollection[] m_TextureCollection;
        public TextureCollection[] TextureCollection { get { return m_TextureCollection; } set { m_TextureCollection = value; } }

        public Texture2D[] GetTextures()
        {
            Texture2D[] textures = new Texture2D[6];
            textures[0] = Textures.Top;
            textures[1] = Textures.Bottom;
            textures[2] = Textures.Back;
            textures[3] = Textures.North;
            textures[4] = Textures.East;
            textures[5] = Textures.West;

            return textures;
        }

        public virtual void OnInit() { }

        public static Dev_BlockConfig CreateAirBlockConfig(World world)
        {
            return new Dev_BlockConfig()
            {
                BlockName = "air",
                Type = BlockProvider.AIR_TYPE,
                Solid = false,
                Transparent = true,
                PhysicMaterialID = -1
            };
        }

        [System.Obsolete("Use OnSetup without config parameter.")]
        public virtual bool OnSetUp(Hashtable config, World world)
        {
            return true;
        }

        public virtual bool OnSetUp(World world)
        {
            m_TextureCollection = new TextureCollection[6];
            Texture2D[] textures = GetTextures();
            for (int i = 0; i < 6; i++)
                m_TextureCollection[i] = world.TextureProvider.GetTextureCollection(textures[i]);

            return true;
        }

        [System.Obsolete("Never used. Try to remove.")]
        public virtual bool OnPostSetUp(World world)
        {
            return true;
        }

        public override string ToString()
        {
            return BlockName;
        }
    }
}
