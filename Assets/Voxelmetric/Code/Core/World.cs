using System.Collections.Generic;
using UnityEngine;
using Voxelmetric.Code.Configurable.Structures;
using Voxelmetric.Code.Core.Operations;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Load_Resources;
using Voxelmetric.Code.Load_Resources.Blocks;
using Voxelmetric.Code.Load_Resources.Textures;
using Voxelmetric.Code.VM;

namespace Voxelmetric.Code.Core
{
    public class World : MonoBehaviour
    {
        [SerializeField]
        private string m_WorldConfig = "default";
        public string WorldConfig { get { return m_WorldConfig; } set { m_WorldConfig = value; } }
        [SerializeField]
        private WorldConfig m_Config;
        public WorldConfig Config { get { return m_Config; } set { m_Config = value; } }

        //This world name is used for the save file name and as a seed for random noise
        [SerializeField]
        private string m_WorldName = "world";
        public string WorldName { get { return m_WorldName; } set { m_WorldName = value; } }

        private WorldChunks m_Chunks;
        public WorldChunks Chunks { get { return m_Chunks; } set { m_Chunks = value; } }
        private WorldBlocks m_Blocks;
        public WorldBlocks Blocks { get { return m_Blocks; } set { m_Blocks = value; } }
        [SerializeField]
        private VmNetworking m_Networking = new VmNetworking();
        public VmNetworking Networking { get { return m_Networking; } set { m_Networking = value; } }

        private BlockProvider m_BlockProvider;
        public BlockProvider BlockProvider { get { return m_BlockProvider; } set { m_BlockProvider = value; } }
        private TextureProvider m_TextureProvider;
        public TextureProvider TextureProvider { get { return m_TextureProvider; } set { m_TextureProvider = value; } }
        private TerrainGen m_TerrainGen;
        public TerrainGen TerrainGen { get { return m_TerrainGen; } set { m_TerrainGen = value; } }

        [SerializeField]
        private Material[] m_RenderMaterials;
        public Material[] RenderMaterials { get { return m_RenderMaterials; } set { m_RenderMaterials = value; } }
        [SerializeField]
        private PhysicMaterial[] m_PhysicsMaterials;
        public PhysicMaterial[] PhysicMaterials { get { return m_PhysicsMaterials; } set { m_PhysicsMaterials = value; } }

        public AABBInt Bounds { get; set; }

        private readonly List<ModifyBlockContext> modifyRangeQueue = new List<ModifyBlockContext>();

        private readonly object pendingStructureMutex = new object();
        private readonly Dictionary<Vector3Int, List<StructureContext>> pendingStructures = new Dictionary<Vector3Int, List<StructureContext>>();
        private readonly List<StructureInfo> pendingStructureInfo = new List<StructureInfo>();

        public bool CheckInsideWorld(Vector3Int pos)
        {
            int offsetX = (Bounds.maxX + Bounds.minX) >> 1;
            int offsetZ = (Bounds.maxZ + Bounds.minZ) >> 1;

            int xx = (pos.x - offsetX) / Env.CHUNK_SIZE;
            int zz = (pos.z - offsetZ) / Env.CHUNK_SIZE;
            int yy = pos.y / Env.CHUNK_SIZE;
            int horizontalRadius = (Bounds.maxX - Bounds.minX) / (2 * Env.CHUNK_SIZE);

            return xx * xx + zz * zz <= horizontalRadius * horizontalRadius &&
                   yy >= (Bounds.minY / Env.CHUNK_SIZE) && yy <= (Bounds.maxY / Env.CHUNK_SIZE);
        }

        void Awake()
        {
            Chunks = new WorldChunks(this);
            Blocks = new WorldBlocks(this);
        }

        void Start()
        {
            StartWorld();
        }

        void OnApplicationQuit()
        {
            StopWorld();
        }

        public void Configure()
        {
            Config = new ConfigLoader<WorldConfig>(new[] { "Worlds" }).GetConfig(WorldConfig);
            VerifyConfig();

            TextureProvider = Voxelmetric.resources.GetTextureProvider(this);
            BlockProvider = Voxelmetric.resources.GetBlockProvider(this);

            TextureProvider.Init(Config);
            BlockProvider.Init(Config.blockFolder, this);

            foreach (var renderMaterial in RenderMaterials)
            {
                renderMaterial.mainTexture = TextureProvider.Atlas;
            }
        }

        private void VerifyConfig()
        {
            // minX can't be greater then maxX
            if (m_Config.minX > m_Config.maxX)
            {
                int tmp = m_Config.minX;
                m_Config.maxX = m_Config.minX;
                m_Config.minX = tmp;
            }

            if (m_Config.minX != m_Config.maxX)
            {
                // Make sure there is at least one chunk worth of space in the world on the X axis
                if (m_Config.maxX - m_Config.minX < Env.CHUNK_SIZE)
                    m_Config.maxX = m_Config.minX + Env.CHUNK_SIZE;
            }

            // minY can't be greater then maxY
            if (m_Config.minY > m_Config.maxY)
            {
                int tmp = m_Config.minY;
                m_Config.maxY = m_Config.minY;
                m_Config.minY = tmp;
            }

            if (m_Config.minY != m_Config.maxY)
            {
                // Make sure there is at least one chunk worth of space in the world on the Y axis
                if (m_Config.maxY - m_Config.minY < Env.CHUNK_SIZE)
                    m_Config.maxY = m_Config.minY + Env.CHUNK_SIZE;
            }

            // minZ can't be greater then maxZ
            if (m_Config.minZ > m_Config.maxZ)
            {
                int tmp = m_Config.minZ;
                m_Config.maxZ = m_Config.minZ;
                m_Config.minZ = tmp;
            }

            if (m_Config.minZ != m_Config.maxZ)
            {
                // Make sure there is at least one chunk worth of space in the world on the Z axis
                if (m_Config.maxZ - m_Config.minZ < Env.CHUNK_SIZE)
                    m_Config.maxZ = m_Config.minZ + Env.CHUNK_SIZE;
            }
        }

        private void StartWorld()
        {
            Configure();

            Networking.StartConnections(this);
            TerrainGen = TerrainGen.Create(this, Config.layerFolder);
        }

        private void StopWorld()
        {
            Networking.EndConnections();
        }

        public void CapCoordXInsideWorld(ref int minX, ref int maxX)
        {
            if (Config.minX != Config.maxX)
            {
                minX = Mathf.Max(minX, Config.minX);
                maxX = Mathf.Min(maxX, Config.maxX);
            }
        }

        public void CapCoordYInsideWorld(ref int minY, ref int maxY)
        {
            if (Config.minY != Config.maxY)
            {
                minY = Mathf.Max(minY, Config.minY);
                maxY = Mathf.Min(maxY, Config.maxY);
            }
        }

        public void CapCoordZInsideWorld(ref int minZ, ref int maxZ)
        {
            if (Config.minZ != Config.maxZ)
            {
                minZ = Mathf.Max(minZ, Config.minZ);
                maxZ = Mathf.Min(maxZ, Config.maxZ);
            }
        }

        public bool IsCoordInsideWorld(ref Vector3Int pos)
        {
            return
                Config.minX == Config.maxX || (pos.x >= Config.minX && pos.x <= Config.maxX) ||
                Config.minY == Config.maxY || (pos.y >= Config.minY && pos.y <= Config.maxY) ||
                Config.minZ == Config.maxZ || (pos.z >= Config.minZ && pos.z <= Config.maxZ);
        }

        public void RegisterModifyRange(ModifyBlockContext onModified)
        {
            modifyRangeQueue.Add(onModified);
        }

        public void PerformBlockActions()
        {
            for (int i = 0; i < modifyRangeQueue.Count; i++)
                modifyRangeQueue[i].PerformAction();

            modifyRangeQueue.Clear();
        }

        public void RegisterPendingStructure(StructureInfo info, StructureContext context)
        {
            if (info == null || context == null)
                return;

            lock (pendingStructureMutex)
            {
                {
                    bool alreadyThere = false;

                    // Do not register the same thing twice
                    for (int i = 0; i < pendingStructureInfo.Count; i++)
                    {
                        if (pendingStructureInfo[i].Equals(info))
                        {
                            alreadyThere = true;
                            break;
                        }
                    }

                    if (!alreadyThere)
                        pendingStructureInfo.Add(info);
                }

                List<StructureContext> list;
                if (pendingStructures.TryGetValue(context.m_ChunkPos, out list))
                    list.Add(context);
                else
                    pendingStructures.Add(context.m_ChunkPos, new List<StructureContext> { context });
            }

            {
                Chunk chunk;
                lock (Chunks)
                {
                    // Let the chunk know it needs an update if it exists
                    chunk = Chunks.Get(ref context.m_ChunkPos);
                }
                if (chunk != null)
                    chunk.NeedApplyStructure = true;
            }
        }

        public void UnregisterPendingStructures()
        {
            // TODO: This is not exactly optimal. A lot of iterations for one mutex. On the other hand, I expect only
            // a small amount of structures stored here. Definitelly not hundreds or more. But there's a room for
            // improvement...
            lock (pendingStructureMutex)
            {
                // Let's see whether we can unload any positions
                for (int i = 0; i < pendingStructureInfo.Count;)
                {
                    var info = pendingStructureInfo[i];
                    var pos = info.chunkPos;

                    // See whether we can remove the structure
                    if (!Bounds.IsInside(ref pos))
                        pendingStructureInfo.RemoveAt(i);
                    else
                    {
                        ++i;
                        continue;
                    }

                    // Structure removed. We need to remove any associated world positions now
                    for (int y = info.bounds.minY; y < info.bounds.maxY; y += Env.CHUNK_SIZE)
                    {
                        for (int z = info.bounds.minZ; z < info.bounds.maxZ; z += Env.CHUNK_SIZE)
                        {
                            for (int x = info.bounds.minX; x < info.bounds.maxX; x += Env.CHUNK_SIZE)
                            {
                                List<StructureContext> list;
                                if (!pendingStructures.TryGetValue(new Vector3Int(x, y, z), out list) || list.Count <= 0)
                                    continue;

                                // Remove any occurence of this structure from pending positions
                                for (int j = 0; j < list.Count;)
                                {
                                    if (list[j].id == info.id)
                                        list.RemoveAt(j);
                                    else
                                        ++j;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void ApplyPendingStructures(Chunk chunk)
        {
            // Check this unlocked first
            if (!chunk.NeedApplyStructure)
                return;

            List<StructureContext> list;
            int cnt;

            lock (pendingStructureMutex)
            {
                if (!chunk.NeedApplyStructure)
                    return;

                // Consume the event
                chunk.NeedApplyStructure = false;

                if (!pendingStructures.TryGetValue(chunk.Pos, out list))
                    return;

                cnt = list.Count;
            }

            // Apply changes to the chunk
            for (int i = chunk.MaxPendingStructureListIndex; i < cnt; i++)
                list[i].Apply(chunk);
            chunk.MaxPendingStructureListIndex = cnt - 1;
        }
    }
}
