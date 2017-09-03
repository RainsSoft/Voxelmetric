using System;
using System.Collections;
using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Load_Resources.Blocks;

/// <summary>
/// BlockConfigs define constants for block types. Things like if the block is solid,
/// the block's texture etc. We could have used static variables in the block class
/// but the same block class can be used by different blocks - for example block cube
/// can be used by any cube block with a different texture for each block by defining the
/// texture for each of them in the block's json config. Then a BlockConfig will be
/// created for each block type and stored in BlockIndex referenced by the block type.
/// </summary>
public class BlockConfig
{
    //! Block type. Set externally by BlockIndex class when config is loaded
    public ushort type = 1;

    #region Parameters read from config

    //! Unique identifier of block config
    public ushort TypeInConfig { get; protected set; }
    //! Unique identifier of block config
    public string Name { get; protected set; }

    private string m_ClassName;
    public string ClassName
    {
        get { return m_ClassName; }
        set
        {
            m_ClassName = value;
            BlockClass = Type.GetType(value + ", " + typeof(Block).Assembly, false);
        }
    }

    public Type BlockClass { get; protected set; }

    public bool Solid { get; protected set; }
    public bool Transparent { get; protected set; }
    public bool RaycastHit { get; protected set; }
    public bool RaycastHitOnRemoval { get; protected set; }
    public int RenderMaterialID { get; protected set; }
    public int PhysicMaterialID { get; protected set; }

    #endregion

    public static BlockConfig CreateAirBlockConfig(World world)
    {
        return new BlockConfig
        {
            Name = "air",
            TypeInConfig = BlockProvider.AIR_TYPE,
            ClassName = "Block",
            Solid = false,
            Transparent = true,
            PhysicMaterialID = -1
        };
    }

    /// <summary>
    /// Assigns the variables in the config from a hashtable. When overriding this
    /// remember to call the base function first.
    /// </summary>
    /// <param name="config">Hashtable of the json config for the block</param>
    /// <param name="world">The world this block type belongs to</param>
    public virtual bool OnSetUp(Hashtable config, World world)
    {
        // Obligatory parameters
        {
            string tmpName;
            if (!_GetPropertyFromConfig(config, "name", out tmpName))
            {
                Debug.LogError("Parameter 'name' missing from config");
                return false;
            }
            Name = tmpName;

            long tmpTypeInConfig;
            if (!_GetPropertyFromConfig(config, "type", out tmpTypeInConfig))
            {
                Debug.LogError("Parameter 'type' missing from config");
                return false;
            }
            TypeInConfig = (ushort)tmpTypeInConfig;
        }

        // Optional parameters
        {
            ClassName = _GetPropertyFromConfig(config, "blockClass", "Block");
            Solid = _GetPropertyFromConfig(config, "solid", true);
            Transparent = _GetPropertyFromConfig(config, "transparent", false);
            RaycastHit = _GetPropertyFromConfig(config, "raycastHit", Solid);
            RaycastHitOnRemoval = _GetPropertyFromConfig(config, "raycastHitOnRemoval", Solid);

            // Try to associate requested render materials with one of world's materials
            {
                RenderMaterialID = 0;
                string materialName = _GetPropertyFromConfig(config, "material", "");
                for (int i = 0; i < world.RenderMaterials.Length; i++)
                    if (world.RenderMaterials[i].name.Equals(materialName))
                    {
                        RenderMaterialID = i;
                        break;
                    }
            }

            // Try to associate requested physic materials with one of world's materials
            {
                PhysicMaterialID = Solid ? 0 : -1; // solid objects will collide by default
                string materialName = _GetPropertyFromConfig(config, "materialPx", "");
                for (int i = 0; i < world.PhysicMaterials.Length; i++)
                    if (world.PhysicMaterials[i].name.Equals(materialName))
                    {
                        PhysicMaterialID = i;
                        break;
                    }
            }
        }

        return true;
    }

    public virtual bool OnPostSetUp(World world)
    {
        return true;
    }

    public override string ToString()
    {
        return Name;
    }

    protected static bool _GetPropertyFromConfig<T>(Hashtable config, string key, out T ret)
    {
        if (config.ContainsKey(key))
        {
            ret = (T)config[key];
            return true;
        }

        ret = default(T);
        return false;
    }

    protected static T _GetPropertyFromConfig<T>(Hashtable config, string key, T defaultValue)
    {
        if (config.ContainsKey(key))
            return (T)config[key];

        return defaultValue;
    }
}
