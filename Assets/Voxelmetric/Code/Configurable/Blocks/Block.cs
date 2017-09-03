using UnityEngine;
using Voxelmetric.Code.Configurable.Blocks;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Load_Resources.Blocks;

public class Block
{
    protected BlockConfig m_Config;
    private string m_Name;
    public string Name { get { return m_Name; } set { m_Name = value; } }
    private ushort m_Type;
    public ushort Type { get { return m_Type; } set { m_Type = value; } }
    private int m_RenderMaterialID;
    public int RenderMaterialID { get { return m_RenderMaterialID; } set { m_RenderMaterialID = value; } }
    private int m_PhysicMaterialID;
    public int PhysicMaterialID { get { return m_PhysicMaterialID; } set { m_PhysicMaterialID = value; } }
    private bool m_Solid;
    public bool Solid { get { return m_Solid; } set { m_Solid = value; } }
    private bool m_Custom;
    public bool Custom { get { return m_Custom; } set { m_Custom = value; } }

    public bool CanCollide { get { return PhysicMaterialID >= 0; } }

    public Block()
    {
        Type = 0;
        m_Config = null;
    }

    public void Init(ushort type, BlockConfig config)
    {
        Type = type;
        this.m_Config = config;

        RenderMaterialID = config.RenderMaterialID;
        PhysicMaterialID = config.PhysicMaterialID;

        m_Name = config.Name;
        Solid = config.Solid;
        Custom = false;
    }

    public virtual string DisplayName
    {
        get { return m_Name; }
    }

    public virtual void OnInit(BlockProvider blockProvider)
    {
    }

    public virtual void BuildBlock(Chunk chunk, ref Vector3Int localpos, int materialID)
    {
    }

    public bool CanBuildFaceWith(Block adjacentBlock)
    {
        return adjacentBlock.Solid ? !Solid : (Solid || Type != adjacentBlock.Type);
    }

    public virtual void BuildFace(Chunk chunk, Vector3[] vertices, ref BlockFace face, bool rotated)
    {
    }

    public virtual void OnCreate(Chunk chunk, ref Vector3Int localPos)
    {
    }

    public virtual void OnDestroy(Chunk chunk, ref Vector3Int localPos)
    {
    }

    public virtual void RandomUpdate(Chunk chunk, ref Vector3Int localPos)
    {
    }

    public virtual void ScheduledUpdate(Chunk chunk, ref Vector3Int localPos)
    {
    }

    public bool RaycastHit(ref Vector3 pos, ref Vector3 dir, ref Vector3Int bPos, bool removalRequested)
    {
        return removalRequested ? m_Config.RaycastHitOnRemoval : m_Config.RaycastHit;
    }

    public override string ToString()
    {
        return m_Name;
    }
}
