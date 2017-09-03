using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
// ReSharper disable All

namespace Voxelmetric.Code.Load_Resources.Blocks
{
    public class BlockProvider
    {
        //! Air type block will always be present
        public const ushort AIR_TYPE = 0;
        public static readonly BlockData airBlock = new BlockData(AIR_TYPE, false);

        //! An array of loaded block configs
        private BlockConfig[] m_Configs;

        //! Mapping from config's name to type
        private readonly Dictionary<string, ushort> names;
        //! Mapping from typeInConfig to type
        private ushort[] m_Types;

        public Block[] BlockTypes { get; private set; }

        public static BlockProvider Create()
        {
            return new BlockProvider();
        }

        private BlockProvider()
        {
            names = new Dictionary<string, ushort>();
        }

        public void Init(string blockFolder, World world)
        {
            // Add all the block definitions defined in the config files
            ProcessConfigs(world, blockFolder);

            // Build block type lookup table
            BlockTypes = new Block[m_Configs.Length];
            for (int i = 0; i < m_Configs.Length; i++)
            {
                BlockConfig config = m_Configs[i];

                Block block = (Block)Activator.CreateInstance(config.BlockClass);
                block.Init((ushort)i, config);
                BlockTypes[i] = block;
            }

            // Once all blocks are set up, call OnInit on them. It is necessary to do it in a separate loop
            // in order to ensure there will be no dependency issues.
            for (int i = 0; i < BlockTypes.Length; i++)
            {
                Block block = BlockTypes[i];
                block.OnInit(this);
            }

            // Add block types from config
            foreach (var configFile in m_Configs)
            {
                configFile.OnPostSetUp(world);
            }
        }

        // World is only needed for setting up the textures
        private void ProcessConfigs(World world, string blockFolder)
        {
            var configFiles = Resources.LoadAll<TextAsset>(blockFolder);
            List<BlockConfig> configs = new List<BlockConfig>(configFiles.Length);
            Dictionary<ushort, ushort> types = new Dictionary<ushort, ushort>();

            // Add the static air block type
            AddBlockType(configs, types, BlockConfig.CreateAirBlockConfig(world));

            // Add block types from config
            foreach (var configFile in configFiles)
            {
                Hashtable configHash = JsonConvert.DeserializeObject<Hashtable>(configFile.text);

                Type configType = Type.GetType(configHash["configClass"] + ", " + typeof(BlockConfig).Assembly, false);
                if (configType == null)
                {
                    Debug.LogError("Could not create config for " + configHash["configClass"]);
                    continue;
                }

                BlockConfig config = (BlockConfig)Activator.CreateInstance(configType);
                if (!config.OnSetUp(configHash, world))
                    continue;

                if (!VerifyBlockConfig(types, config))
                    continue;

                AddBlockType(configs, types, config);
            }

            m_Configs = configs.ToArray();

            // Now iterate over conigs and find the one with the highest TypeInConfig
            ushort maxTypeInConfig = AIR_TYPE;
            for (int i = 0; i < m_Configs.Length; i++)
            {
                if (m_Configs[i].TypeInConfig > maxTypeInConfig)
                    maxTypeInConfig = m_Configs[i].TypeInConfig;
            }

            // Allocate maxTypeInConfigs big array now and map config types to runtime types
            m_Types = new ushort[maxTypeInConfig + 1];
            for (ushort i = 0; i < m_Configs.Length; i++)
            {
                m_Types[m_Configs[i].TypeInConfig] = i;
            }
        }

        private bool VerifyBlockConfig(Dictionary<ushort, ushort> types, BlockConfig config)
        {
            // Unique identifier of block type
            if (names.ContainsKey(config.Name))
            {
                Debug.LogErrorFormat("Two blocks with the name {0} are defined", config.Name);
                return false;
            }

            // Unique identifier of block type
            if (types.ContainsKey(config.TypeInConfig))
            {
                Debug.LogErrorFormat("Two blocks with type {0} are defined", config.TypeInConfig);
                return false;
            }

            // Class name must be valid
            if (config.BlockClass == null)
            {
                Debug.LogErrorFormat("Invalid class name {0} for block {1}", config.ClassName, config.Name);
                return false;
            }

            // Use the type defined in the config if there is one, otherwise add one to the largest index so far
            if (config.type == ushort.MaxValue)
            {
                Debug.LogError("Maximum number of block types reached for " + config.Name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds a block type to the index and adds it's name to a dictionary for quick lookup
        /// </summary>
        /// <param name="configs">A list of configs</param>
        /// <param name="types"></param>
        /// <param name="config">The controller object for this block</param>
        /// <returns>The index of the block</returns>
        private void AddBlockType(List<BlockConfig> configs, Dictionary<ushort, ushort> types, BlockConfig config)
        {
            config.type = (ushort)configs.Count;
            configs.Add(config);
            names.Add(config.Name, config.type);
            types.Add(config.TypeInConfig, config.type);
        }

        public ushort GetType(string name)
        {
            ushort type;
            if (names.TryGetValue(name, out type))
                return type;

            Debug.LogError("Block not found: " + name);
            return AIR_TYPE;
        }

        public ushort GetTypeFromTypeInConfig(ushort typeInConfig)
        {
            if (typeInConfig < m_Types.Length)
                return m_Types[typeInConfig];

            Debug.LogError("TypeInConfig not found: " + typeInConfig);
            return AIR_TYPE;
        }

        public Block GetBlock(string name)
        {
            ushort type;
            if (names.TryGetValue(name, out type))
                return BlockTypes[type];

            Debug.LogError("Block not found: " + name);
            return BlockTypes[AIR_TYPE];
        }

        public BlockConfig GetConfig(ushort type)
        {
            if (type < m_Configs.Length)
                return m_Configs[type];

            Debug.LogError("Config not found: " + type);
            return m_Configs[AIR_TYPE];
        }
    }
}
