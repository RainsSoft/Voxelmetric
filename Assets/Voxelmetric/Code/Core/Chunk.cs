using UnityEngine;
using UnityEngine.Profiling;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.MemoryPooling;
using Voxelmetric.Code.Core.GeometryHandler;
using Voxelmetric.Code.Core.StateManager;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core
{
    public sealed class Chunk
    {
        //! ID used by memory pools to map the chunk to a given thread. Must be accessed from the main thread
        private static int id = 0;

        public World World { get; private set; }
        public ChunkStateManagerClient StateManager { get; set; }
        public ChunkBlocks Blocks { get; private set; }
        public ChunkLogic Logic { get; private set; }
        public ChunkRenderGeometryHandler GeometryHandler { get; private set; }
        public ChunkColliderGeometryHandler ChunkColliderGeometryHandler { get; private set; }
        public LocalPools Pools { get; private set; }

        private bool m_NeedApplyStructure;
        public bool NeedApplyStructure { get { return m_NeedApplyStructure; } set { m_NeedApplyStructure = value; } }
        private int m_MaxPendingStructureListIndex;
        public int MaxPendingStructureListIndex { get { return m_MaxPendingStructureListIndex; } set { m_MaxPendingStructureListIndex = value; } }

        //! Chunk position in world coordinates
        public Vector3Int Pos { get; private set; }

        //! Bounding box in world coordinates
        public AABB WorldBounds { get; private set; }

        //! ThreadID associated with this chunk. Used when working with object pools in MT environment. Resources
        //! need to be release where they were allocated. Thanks to this, associated containers could be made lock-free
        public int ThreadID { get; private set; }

        //! Says whether the chunk needs its collider rebuilt
        private bool m_NeedsCollider;
        public bool NeedsCollider
        {
            get
            {
                return m_NeedsCollider;
            }
            set
            {
                bool prevNeedCollider = m_NeedsCollider;
                m_NeedsCollider = value;
                if (m_NeedsCollider && !prevNeedCollider)
                    Blocks.RequestCollider();
            }
        }

        private int m_SideSize = 0;
        public int SideSize
        {
            get { return m_SideSize; }
        }

        public static Chunk CreateChunk(World world, Vector3Int pos)
        {
            Chunk chunk = Globals.MemPools.chunkPool.Pop();
            chunk.Init(world, pos);
            return chunk;
        }

        /// <summary>
        /// Returns the position of the chunk containing this block
        /// </summary>
        /// <returns>The position of the chunk containing this block</returns>
        public static Vector3Int ContainingChunkPos(ref Vector3Int pos)
        {
            return new Vector3Int(
                Helpers.MakeChunkCoordinate(pos.x),
                Helpers.MakeChunkCoordinate(pos.y),
                Helpers.MakeChunkCoordinate(pos.z)
                );
        }

        public static void RemoveChunk(Chunk chunk)
        {
            // Reset the chunk back to defaults
            chunk.Reset();
            chunk.World = null; // Can't do inside Reset!!

            // Return the chunk pack to object pool
            Globals.MemPools.chunkPool.Push(chunk);
        }

        public Chunk(int sideSize = Env.CHUNK_SIZE)
        {
            m_SideSize = sideSize;

            // Associate Chunk with a certain thread and make use of its memory pool
            // This is necessary in order to have lock-free caches
            ThreadID = Globals.WorkPool.GetThreadIDFromIndex(id++);
            Pools = Globals.WorkPool.GetPool(ThreadID);

            StateManager = new ChunkStateManagerClient(this);
            Blocks = new ChunkBlocks(this, sideSize);
        }

        public void Init(World world, Vector3Int pos)
        {
            this.World = world;
            this.Pos = pos;

            StateManager = new ChunkStateManagerClient(this);
            Logic = world.Config.randomUpdateFrequency > 0.0f ? new ChunkLogic(this) : null;

            if (GeometryHandler == null)
                GeometryHandler = new ChunkRenderGeometryHandler(this, world.RenderMaterials);
            if (ChunkColliderGeometryHandler == null)
                ChunkColliderGeometryHandler = new ChunkColliderGeometryHandler(this, world.PhysicMaterials);

            WorldBounds = new AABB(
                pos.x, pos.y, pos.z,
                pos.x + m_SideSize, pos.y + m_SideSize, pos.z + m_SideSize
                );

            Reset();

            Blocks.Init();
            StateManager.Init();
        }

        private void Reset()
        {
            NeedApplyStructure = true;
            MaxPendingStructureListIndex = 0;

            StateManager.Reset();
            Blocks.Reset();
            if (Logic != null)
                Logic.Reset();

            GeometryHandler.Reset();
            ChunkColliderGeometryHandler.Reset();

            m_NeedsCollider = false;
        }

        public bool CanUpdate
        {
            get { return StateManager.CanUpdate(); }
        }

        public void UpdateState()
        {
            // Do not update our chunk until it has all its data prepared
            if (StateManager.IsStateCompleted(ChunkState.Generate))
            {
                // Apply pending structures
                World.ApplyPendingStructures(this);

                // Update logic
                if (Logic != null)
                    Logic.Update();

                // Update blocks
                Blocks.Update();
            }

            // Process chunk tasks
            StateManager.Update();
        }

        public bool UpdateCollisionGeometry()
        {
            // Release the collider when no longer needed
            if (!NeedsCollider)
            {
                StateManager.SetColliderBuilt();
                ChunkColliderGeometryHandler.Reset();
                return false;
            }

            // Build collision geometry only if there is enough time
            if (!Globals.GeometryBudget.HasTimeBudget)
                return false;

            // Build collider if necessary
            if (StateManager.IsStateCompleted(ChunkState.BuildCollider))
            {
                Profiler.BeginSample("UpdateCollisionGeometry");
                Globals.GeometryBudget.StartMeasurement();

                StateManager.SetColliderBuilt();
                ChunkColliderGeometryHandler.Commit();

                Globals.GeometryBudget.StopMeasurement();
                Profiler.EndSample();
                return true;
            }

            return false;
        }

        public bool UpdateRenderGeometry()
        {
            // Build render geometry only if there is enough time
            if (!Globals.GeometryBudget.HasTimeBudget)
                return false;

            // Build chunk mesh if necessary
            if (StateManager.IsStateCompleted(ChunkState.BuildVertices | ChunkState.BuildVerticesNow))
            {
                Profiler.BeginSample("UpdateRenderGeometry");
                Globals.GeometryBudget.StartMeasurement();

                StateManager.SetMeshBuilt();
                GeometryHandler.Commit();

                Globals.GeometryBudget.StopMeasurement();
                Profiler.EndSample();
                return true;
            }

            return false;
        }
    }
}
