using System;
using UnityEngine;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.Events;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.Threading;
using Voxelmetric.Code.Common.Threading.Managers;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Core.StateManager
{
    /// <summary>
    /// Handles state changes for chunks from a client's perspective.
    /// This means there chunk geometry rendering and chunk neighbors
    /// need to be taken into account.
    /// </summary>
    public class ChunkStateManagerClient : ChunkStateManager
    {
        //! Says whether or not the chunk is visible
        public bool Visible
        {
            get
            {
                return Chunk.GeometryHandler.Batcher.Enabled;
            }
            set
            {
                var batcher = Chunk.GeometryHandler.Batcher;
                bool prev = batcher.Enabled;

                if (!value && prev)
                    // Chunk made invisible. We no longer need to build geometry for it
                    m_PendingStates = m_PendingStates.Reset(CurrStateBuildVertices);
                else if (value && !prev)
                    // Chunk made visible. Make a request
                    m_PendingStates = m_PendingStates.Set(ChunkState.BuildVertices);

                batcher.Enabled = value;
            }
        }
        //! Says whether or not building of geometry can be triggered
        public bool PossiblyVisible { get; set; }

        //! State to notify external listeners about
        private ChunkStateExternal m_StateExternal;

        //! If true, edges are to be synchronized with neighbor chunks
        private bool m_SyncEdgeBlocks;

        //! Static shared pointers to callbacks
        private static readonly Action<ChunkStateManagerClient> actionOnLoadData = OnLoadData;
        private static readonly Action<ChunkStateManagerClient> actionOnPrepareGenerate = OnPrepareGenerate;
        private static readonly Action<ChunkStateManagerClient> actionOnGenerateData = OnGenerateData;
        private static readonly Action<ChunkStateManagerClient> actionOnPrepareSaveData = OnPrepareSaveData;
        private static readonly Action<ChunkStateManagerClient> actionOnSaveData = OnSaveData;
        private static readonly Action<ChunkStateManagerClient> actionOnBuildVertices = OnBuildVertices;
        private static readonly Action<ChunkStateManagerClient> actionOnBuildCollider = OnBuildCollider;

        //! Flags telling us whether pool items should be returned back to the pool
        private ChunkPoolItemState m_PoolState;
        private ITaskPoolItem m_ThreadPoolItem;

        public ChunkStateManagerClient(Chunk chunk) : base(chunk) { }

        public override void Init()
        {
            base.Init();

            // Subscribe neighbors
            SubscribeNeighbors(true);
        }

        public override void Reset()
        {
            base.Reset();

            SubscribeNeighbors(false);

            m_StateExternal = ChunkStateExternal.None;

            Visible = false;
            PossiblyVisible = false;

            m_SyncEdgeBlocks = true;

            m_PoolState = m_PoolState.Reset();
            m_ThreadPoolItem = null;
        }

        public override void SetMeshBuilt()
        {
            m_CompletedStates = m_CompletedStatesSafe = m_CompletedStates.Reset(CurrStateBuildVertices);
        }

        public override void SetColliderBuilt()
        {
            m_CompletedStates = m_CompletedStatesSafe = m_CompletedStates.Reset(CurrStateBuildCollider);
        }

        private void ReturnPoolItems()
        {
            var pools = Globals.MemPools;

            // Global.MemPools is not thread safe and were returning values to it from a different thread.
            // Therefore, each client remembers which pool it used and once the task is finished it returns
            // it back to the pool as soon as possible from the main thread

            if (m_PoolState.Check(ChunkPoolItemState.ThreadPI))
                pools.SMThreadPI.Push(m_ThreadPoolItem as ThreadPoolItem<ChunkStateManagerClient>);
            else if (m_PoolState.Check(ChunkPoolItemState.TaskPI))
                pools.SMTaskPI.Push(m_ThreadPoolItem as TaskPoolItem<ChunkStateManagerClient>);

            m_PoolState = m_PoolState.Reset();
            m_ThreadPoolItem = null;
        }

        public override void Update()
        {
            // Return processed work items back to the pool
            ReturnPoolItems();

            if (m_StateExternal != ChunkStateExternal.None)
            {
                // Notify everyone listening
                NotifyAll(m_StateExternal);

                m_StateExternal = ChunkStateExternal.None;
            }

            // If removal was requested before we got to loading the chunk at all we can safely mark
            // it as removed right away
            if (m_RemovalRequested && !m_CompletedStates.Check(ChunkState.LoadData))
            {
                m_CompletedStates = m_CompletedStates.Set(ChunkState.Remove);
                return;
            }

            // Go from the least important bit to most important one. If a given bit it set
            // we execute the task tied with it
            ProcessNotifyState();
            if (m_PendingStates != 0)
            {
                // In order to save performance, we generate chunk data on-demand - when the chunk can be seen
                if (PossiblyVisible)
                {
                    if (m_PendingStates.Check(ChunkState.LoadData) && LoadData())
                        return;

                    ProcessNotifyState();
                }

                if (m_PendingStates.Check(ChunkState.PrepareGenerate) && PrepareGenerate())
                    return;

                ProcessNotifyState();
                if (m_PendingStates.Check(ChunkState.Generate) && GenerateData())
                    return;

                ProcessNotifyState();
                if (m_PendingStates.Check(ChunkState.PrepareSaveData) && PrepareSaveData())
                    return;

                ProcessNotifyState();
                if (m_PendingStates.Check(ChunkState.SaveData) && SaveData())
                    return;

                ProcessNotifyState();
                if (m_PendingStates.Check(ChunkState.Remove) && RemoveChunk())
                    return;

                ProcessNotifyState();
                if (m_PendingStates.Check(ChunkState.BuildCollider) && BuildCollider())
                    return;

                // In order to save performance, we generate geometry on-demand - when the chunk can be seen
                if (Visible)
                {
                    ProcessNotifyState();
                    if (m_PendingStates.Check(CurrStateBuildVertices))
                        BuildVertices();
                }
            }
        }

        private void ProcessNotifyState()
        {
            if (m_NextState == ChunkState.Idle)
                return;

            OnNotified(this, m_NextState);
            m_NextState = ChunkState.Idle;
        }

        public override void OnNotified(IEventSource<ChunkState> source, ChunkState state)
        {
            // Enqueue the request
            m_PendingStates = m_PendingStates.Set(state);
        }

        #region Load chunk data

        private const ChunkState CurrStateLoadData = ChunkState.LoadData;
        private const ChunkState NextStateLoadData = ChunkState.PrepareGenerate;

        private static void OnLoadData(ChunkStateManagerClient stateManager)
        {
            bool success = Serialization.Serialization.Read(stateManager.save);
            OnLoadDataDone(stateManager, success);
        }

        private static void OnLoadDataDone(ChunkStateManagerClient stateManager, bool success)
        {
            if (success)
            {
                stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateLoadData);
                stateManager.m_NextState = NextStateLoadData;
            }
            else
            {
                stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateLoadData | ChunkState.PrepareGenerate);
                stateManager.m_NextState = ChunkState.Generate;
            }

            stateManager.m_TaskRunning = false;
        }

        private bool LoadData()
        {
            m_PendingStates = m_PendingStates.Reset(CurrStateLoadData);
            m_CompletedStates = m_CompletedStates.Reset(CurrStateLoadData);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Features.USE_SERIALIZATION)
            {
                var task = Globals.MemPools.SMTaskPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.TaskPI);
                m_ThreadPoolItem = task;
                task.Set(actionOnLoadData, this);

                m_TaskRunning = true;
                IOPoolManager.Add(m_ThreadPoolItem);

                return true;
            }

            //OnLoadDataDone(this, false);
            //return false;
        }

        #endregion Load chunk data

        #region Prepare generate

        private const ChunkState CurrStatePrepareGenerate = ChunkState.PrepareGenerate;
        private const ChunkState NextStatePrepareGenerate = ChunkState.Generate;

        private static void OnPrepareGenerate(ChunkStateManagerClient stateManager)
        {
            bool success = stateManager.save.DoDecompression();
            OnPrepareGenerateDone(stateManager, success);
        }

        private static void OnPrepareGenerateDone(ChunkStateManagerClient stateManager, bool success)
        {
            // Consume info about invalidated chunk
            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStatePrepareGenerate);

            if (success)
            {
                if (stateManager.save.IsDifferential)
                {
                    stateManager.m_NextState = NextStatePrepareGenerate;
                }
                else
                {
                    stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(ChunkState.Generate);
                    stateManager.m_NextState = ChunkState.BuildVertices;
                }
            }
            else
            {
                stateManager.m_NextState = NextStatePrepareGenerate;
            }

            stateManager.m_TaskRunning = false;
        }

        private bool PrepareGenerate()
        {
            if (!m_CompletedStates.Check(ChunkState.LoadData))
                return true;

            m_PendingStates = m_PendingStates.Reset(CurrStatePrepareGenerate);
            m_CompletedStates = m_CompletedStates.Reset(CurrStatePrepareGenerate);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Features.USE_SERIALIZATION)
            {
                var task = Globals.MemPools.SMThreadPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.ThreadPI);
                m_ThreadPoolItem = task;
                task.Set(Chunk.ThreadID, actionOnPrepareGenerate, this);

                m_TaskRunning = true;
                IOPoolManager.Add(m_ThreadPoolItem);

                return true;
            }

            //OnPrepareGenerateDone(this, false);
            //return false;
        }

        #endregion

        #region Generate Chunk data

        private const ChunkState CurrStateGenerateData = ChunkState.Generate;
        private const ChunkState NextStateGenerateData = ChunkState.BuildVertices;

        private static void OnGenerateData(ChunkStateManagerClient stateManager)
        {
            Chunk chunk = stateManager.Chunk;
            chunk.World.TerrainGen.GenerateTerrain(chunk);

            // Commit serialization changes if any
            if (Features.USE_SERIALIZATION)
                stateManager.save.CommitChanges();

            // Calculate the amount of non-empty blocks
            chunk.Blocks.CalculateEmptyBlocks();

            //chunk.blocks.Compress();
            //chunk.blocks.Decompress();

            OnGenerateDataDone(stateManager);
        }

        private static void OnGenerateDataDone(ChunkStateManagerClient stateManager)
        {
            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateGenerateData);
            stateManager.m_NextState = NextStateGenerateData;
            stateManager.m_TaskRunning = false;
        }

        public static void OnGenerateDataOverNetworkDone(ChunkStateManagerClient stateManager)
        {
            OnGenerateDataDone(stateManager);
            OnLoadDataDone(stateManager, false); //TODO: change to true once the network layers is implemented properly
        }

        private bool GenerateData()
        {
            if (!m_CompletedStates.Check(ChunkState.LoadData))
                return true;

            m_PendingStates = m_PendingStates.Reset(CurrStateGenerateData);
            m_CompletedStates = m_CompletedStates.Reset(CurrStateGenerateData);
            m_CompletedStatesSafe = m_CompletedStates;

            var task = Globals.MemPools.SMThreadPI.Pop();
            m_PoolState = m_PoolState.Set(ChunkPoolItemState.ThreadPI);
            m_ThreadPoolItem = task;

            task.Set(Chunk.ThreadID, actionOnGenerateData, this);

            m_TaskRunning = true;
            WorkPoolManager.Add(task);

            return true;
        }

        #endregion Generate chunk data

        #region Prepare save

        private const ChunkState CurrStatePrepareSaveData = ChunkState.PrepareSaveData;
        private const ChunkState NextStatePrepareSaveData = ChunkState.SaveData;

        private static void OnPrepareSaveData(ChunkStateManagerClient stateManager)
        {
            bool success = stateManager.save.DoCompression();
            OnPrepareSaveDataDone(stateManager, success);
        }

        private static void OnPrepareSaveDataDone(ChunkStateManagerClient stateManager, bool success)
        {
            if (Features.USE_SERIALIZATION)
            {
                if (!success)
                {
                    // Free temporary memory in case of failure
                    stateManager.save.MarkAsProcessed();

                    // Consider SaveData completed as well
                    stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(NextStatePrepareSaveData);
                    stateManager.m_IsSaveNeeded = false;
                }
                else
                    stateManager.m_NextState = NextStatePrepareSaveData;
            }

            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStatePrepareSaveData);
            stateManager.m_TaskRunning = false;
        }

        private bool PrepareSaveData()
        {
            // We need to wait until chunk is generated
            if (!m_CompletedStates.Check(ChunkState.Generate))
                return true;

            m_PendingStates = m_PendingStates.Reset(CurrStatePrepareSaveData);
            m_CompletedStates = m_CompletedStates.Reset(CurrStatePrepareSaveData);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Features.USE_SERIALIZATION)
            {
                save.ConsumeChanges();

                var task = Globals.MemPools.SMThreadPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.ThreadPI);
                m_ThreadPoolItem = task;
                task.Set(Chunk.ThreadID, actionOnPrepareSaveData, this);

                m_TaskRunning = true;
                IOPoolManager.Add(task);

                return true;
            }

            //OnPrepareSaveDataDone(this, false);
            //return false;
        }

        #endregion Save chunk data

        #region Save chunk data

        private const ChunkState CurrStateSaveData = ChunkState.SaveData;

        private static void OnSaveData(ChunkStateManagerClient stateManager)
        {
            bool success = Serialization.Serialization.Write(stateManager.save);
            OnSaveDataDone(stateManager, success);
        }

        private static void OnSaveDataDone(ChunkStateManagerClient stateManager, bool success)
        {
            if (Features.USE_SERIALIZATION)
            {
                if (success)
                    // Notify listeners in case of success
                    stateManager.m_StateExternal = ChunkStateExternal.Saved;
                else
                {
                    // Free temporary memory in case of failure
                    stateManager.save.MarkAsProcessed();
                    stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(ChunkState.SaveData);
                }
            }

            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateSaveData);
            stateManager.m_IsSaveNeeded = false;
            stateManager.m_TaskRunning = false;
        }

        private bool SaveData()
        {
            // We need to wait until chunk is generated
            if (!m_CompletedStates.Check(ChunkState.PrepareSaveData))
                return true;

            m_PendingStates = m_PendingStates.Reset(CurrStateSaveData);
            m_CompletedStates = m_CompletedStates.Reset(CurrStateSaveData);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Features.USE_SERIALIZATION)
            {
                var task = Globals.MemPools.SMTaskPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.TaskPI);
                m_ThreadPoolItem = task;
                task.Set(actionOnSaveData, this);

                m_TaskRunning = true;
                IOPoolManager.Add(task);

                return true;
            }

            //OnSaveDataDone(this, false);
            //return false;
        }

        #endregion Save chunk data

        private bool SynchronizeNeighbors()
        {
            // 6 neighbors are necessary
            if (ListenerCount != ListenerCountMax)
                return false;

            // All neighbors have to have their data generated
            for (int i = 0; i < Listeners.Length; i++)
            {
                var stateManager = (ChunkStateManagerClient)Listeners[i];
                if (stateManager != null && !stateManager.m_CompletedStates.Check(ChunkState.Generate))
                    return false;
            }

            return true;
        }

        // A dummy chunk. Used e.g. for copying air block to padded area of chunks missing a neighbor
        private static readonly Chunk dummyChunk = new Chunk();

        private void OnSynchronizeEdges()
        {
            int chunkSize1 = Chunk.SideSize - 1;
            int sizePlusPadding = Chunk.SideSize + Env.CHUNK_PADDING;
            int sizeWithPadding = Chunk.SideSize + Env.CHUNK_PADDING_2;
            int sizeWithPaddingPow2 = sizeWithPadding * sizeWithPadding;
            int chunkIterXY = sizeWithPaddingPow2 - sizeWithPadding;

            // Search for neighbors we are vertically aligned with
            for (int i = 0; i < Listeners.Length; i++)
            {
                Chunk neighborChunk = dummyChunk;
                Vector3Int neighborPos;

                var chunkEvent = Listeners[i];
                if (chunkEvent != null)
                {
                    var stateManager = (ChunkStateManagerClient)chunkEvent;
                    neighborChunk = stateManager.Chunk;
                    neighborPos = neighborChunk.Pos;
                }
                else
                {
                    switch ((Direction)i)
                    {
                        case Direction.up: neighborPos = Chunk.Pos.Add(0, Env.CHUNK_SIZE, 0); break;
                        case Direction.down: neighborPos = Chunk.Pos.Add(0, -Env.CHUNK_SIZE, 0); break;
                        case Direction.north: neighborPos = Chunk.Pos.Add(0, 0, Env.CHUNK_SIZE); break;
                        case Direction.south: neighborPos = Chunk.Pos.Add(0, 0, -Env.CHUNK_SIZE); break;
                        case Direction.east: neighborPos = Chunk.Pos.Add(Env.CHUNK_SIZE, 0, 0); break;
                        default: neighborPos = Chunk.Pos.Add(-Env.CHUNK_SIZE, 0, 0); break;
                    }
                }

                // Sync vertical neighbors
                if (neighborPos.x == Chunk.Pos.x && neighborPos.z == Chunk.Pos.z)
                {
                    // Copy the bottom layer of a neighbor chunk to the top layer of ours
                    if (neighborPos.y > Chunk.Pos.y)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(-1, 0, -1);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(-1, Env.CHUNK_SIZE, -1);
                        Chunk.Blocks.Copy(neighborChunk.Blocks, srcIndex, dstIndex, sizeWithPaddingPow2);
                    }
                    // Copy the top layer of a neighbor chunk to the bottom layer of ours
                    else // if (neighborPos.y < chunk.pos.y)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(-1, chunkSize1, -1);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, -1);
                        Chunk.Blocks.Copy(neighborChunk.Blocks, srcIndex, dstIndex, sizeWithPaddingPow2);
                    }
                }

                // Sync front and back neighbors
                if (neighborPos.x == Chunk.Pos.x && neighborPos.y == Chunk.Pos.y)
                {
                    // Copy the front layer of a neighbor chunk to the back layer of ours
                    if (neighborPos.z > Chunk.Pos.z)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, 0);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, Env.CHUNK_SIZE);
                        for (int y = -1;
                             y < sizePlusPadding;
                             y++, srcIndex += chunkIterXY, dstIndex += chunkIterXY)
                        {
                            for (int x = -1; x < sizePlusPadding; x++, srcIndex++, dstIndex++)
                            {
                                BlockData data = neighborChunk.Blocks.Get(srcIndex);
                                Chunk.Blocks.SetRaw(dstIndex, data);
                            }
                        }
                    }
                    // Copy the top back layer of a neighbor chunk to the front layer of ours
                    else // if (neighborPos.z < chunk.pos.z)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, chunkSize1);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, -1);
                        for (int y = -1;
                             y < sizePlusPadding;
                             y++, srcIndex += chunkIterXY, dstIndex += chunkIterXY)
                        {
                            for (int x = -1; x < sizePlusPadding; x++, srcIndex++, dstIndex++)
                            {
                                BlockData data = neighborChunk.Blocks.Get(srcIndex);
                                Chunk.Blocks.SetRaw(dstIndex, data);
                            }
                        }
                    }
                }

                // Sync right and left neighbors
                if (neighborPos.y == Chunk.Pos.y && neighborPos.z == Chunk.Pos.z)
                {
                    // Copy the right layer of a neighbor chunk to the left layer of ours
                    if (neighborPos.x > Chunk.Pos.x)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(0, -1, -1);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(Env.CHUNK_SIZE, -1, -1);
                        for (int y = -1; y < sizePlusPadding; y++)
                        {
                            for (int z = -1;
                                 z < sizePlusPadding;
                                 z++, srcIndex += sizeWithPadding, dstIndex += sizeWithPadding)
                            {
                                BlockData data = neighborChunk.Blocks.Get(srcIndex);
                                Chunk.Blocks.SetRaw(dstIndex, data);
                            }
                        }
                    }
                    // Copy the left layer of a neighbor chunk to the right layer of ours
                    else // if (neighborPos.x < chunk.pos.x)
                    {
                        int srcIndex = Helpers.GetChunkIndex1DFrom3D(chunkSize1, -1, -1);
                        int dstIndex = Helpers.GetChunkIndex1DFrom3D(-1, -1, -1);
                        for (int y = -1; y < sizePlusPadding; y++)
                        {
                            for (int z = -1;
                                 z < sizePlusPadding;
                                 z++, srcIndex += sizeWithPadding, dstIndex += sizeWithPadding)
                            {
                                BlockData data = neighborChunk.Blocks.Get(srcIndex);
                                Chunk.Blocks.SetRaw(dstIndex, data);
                            }
                        }
                    }
                }
            }
        }

        private bool SynchronizeEdges()
        {
            // It is only necessary to perform the sychronization once when data is generated.
            // All subsequend changes of blocks are automatically synchronized inside ChunkBlocks
            if (!m_SyncEdgeBlocks)
                return true;

            // Sync edges if there's enough time
            if (!Globals.EdgeSyncBudget.HasTimeBudget)
                return false;

            m_SyncEdgeBlocks = false;

            Globals.EdgeSyncBudget.StartMeasurement();
            OnSynchronizeEdges();
            Globals.EdgeSyncBudget.StopMeasurement();
            return true;
        }

        private bool SynchronizeChunk()
        {
            // 6 neighbors are necessary to be loaded
            if (!SynchronizeNeighbors())
                return false;

            // Synchronize edge data of chunks
            if (!SynchronizeEdges())
                return false;

            return true;
        }

        #region Build collider geometry

        private const ChunkState CurrStateBuildCollider = ChunkState.BuildCollider;

        private static void OnBuildCollider(ChunkStateManagerClient client)
        {
            Chunk chunk = client.Chunk;
            chunk.ChunkColliderGeometryHandler.Build();
            OnBuildColliderDone(client);
        }

        private static void OnBuildColliderDone(ChunkStateManagerClient stateManager)
        {
            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateBuildCollider);
            stateManager.m_TaskRunning = false;
        }

        /// <summary>
        ///     Build this chunk's collision geometry
        /// </summary>
        private bool BuildCollider()
        {
            if (!m_CompletedStates.Check(ChunkState.Generate))
                return true;

            if (!SynchronizeChunk())
                return true;

            m_PendingStates = m_PendingStates.Reset(CurrStateBuildCollider);
            m_CompletedStates = m_CompletedStates.Reset(CurrStateBuildCollider);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Chunk.Blocks.NonEmptyBlocks > 0)
            {
                var task = Globals.MemPools.SMThreadPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.ThreadPI);
                m_ThreadPoolItem = task;

                task.Set(
                    Chunk.ThreadID,
                    actionOnBuildCollider,
                    this
                    );

                m_TaskRunning = true;
                WorkPoolManager.Add(task);

                return true;
            }

            OnBuildColliderDone(this);
            return false;
        }

        #endregion Generate vertices

        #region Build render geometry

        private const ChunkState CurrStateBuildVertices = ChunkState.BuildVertices | ChunkState.BuildVerticesNow;

        private static void OnBuildVertices(ChunkStateManagerClient client)
        {
            Chunk chunk = client.Chunk;
            chunk.GeometryHandler.Build();
            OnBuildVerticesDone(client);
        }

        private static void OnBuildVerticesDone(ChunkStateManagerClient stateManager)
        {
            stateManager.m_CompletedStates = stateManager.m_CompletedStates.Set(CurrStateBuildVertices);
            stateManager.m_TaskRunning = false;
        }

        /// <summary>
        ///     Build this chunk's geometry
        /// </summary>
        private bool BuildVertices()
        {
            if (!m_CompletedStates.Check(ChunkState.Generate))
                return true;

            if (!SynchronizeChunk())
                return true;

            bool priority = m_PendingStates.Check(ChunkState.BuildVerticesNow);

            m_PendingStates = m_PendingStates.Reset(CurrStateBuildVertices);
            m_CompletedStates = m_CompletedStates.Reset(CurrStateBuildVertices);
            m_CompletedStatesSafe = m_CompletedStates;

            if (Chunk.Blocks.NonEmptyBlocks > 0)
            {
                var task = Globals.MemPools.SMThreadPI.Pop();
                m_PoolState = m_PoolState.Set(ChunkPoolItemState.ThreadPI);
                m_ThreadPoolItem = task;

                task.Set(
                    Chunk.ThreadID,
                    actionOnBuildVertices,
                    this,
                    priority ? Globals.Watch.ElapsedTicks : long.MaxValue
                    );

                m_TaskRunning = true;
                WorkPoolManager.Add(task);

                return true;
            }

            OnBuildVerticesDone(this);
            return false;
        }

        #endregion Generate vertices

        #region Remove chunk

        private static readonly ChunkState CurrStateRemoveChunk = ChunkState.Remove;

        private bool RemoveChunk()
        {
            // If chunk was loaded we need to wait for other states with higher priority to finish first
            if (m_CompletedStates.Check(ChunkState.LoadData))
            {
                // Wait until chunk is generated
                if (!m_CompletedStates.Check(ChunkState.Generate))
                    return false;

                // Wait for save if it was requested
                if (m_IsSaveNeeded)
                    return false;

                m_PendingStates = m_PendingStates.Reset(CurrStateRemoveChunk);
            }

            m_CompletedStates = m_CompletedStates.Set(CurrStateRemoveChunk);
            return true;
        }

        #endregion Remove chunk

        private static void UpdateListenersCount(ChunkStateManagerClient stateManager)
        {
            Chunk chunk = stateManager.Chunk;
            World world = chunk.World;

            // Calculate how many listeners a chunk can have
            int maxListeners = 0;
            if (world.CheckInsideWorld(chunk.Pos.Add(Env.CHUNK_SIZE, 0, 0)) && (chunk.Pos.x != world.Bounds.maxX))
                ++maxListeners;
            if (world.CheckInsideWorld(chunk.Pos.Add(-Env.CHUNK_SIZE, 0, 0)) && (chunk.Pos.x != world.Bounds.minX))
                ++maxListeners;
            if (world.CheckInsideWorld(chunk.Pos.Add(0, Env.CHUNK_SIZE, 0)) && (chunk.Pos.y != world.Bounds.maxY))
                ++maxListeners;
            if (world.CheckInsideWorld(chunk.Pos.Add(0, -Env.CHUNK_SIZE, 0)) && (chunk.Pos.y != world.Bounds.minY))
                ++maxListeners;
            if (world.CheckInsideWorld(chunk.Pos.Add(0, 0, Env.CHUNK_SIZE)) && (chunk.Pos.z != world.Bounds.maxZ))
                ++maxListeners;
            if (world.CheckInsideWorld(chunk.Pos.Add(0, 0, -Env.CHUNK_SIZE)) && (chunk.Pos.z != world.Bounds.minZ))
                ++maxListeners;

            //int prevListeners = stateManager.ListenerCountMax;

            // Update max listeners and request geometry update
            stateManager.ListenerCountMax = maxListeners;

            // Request synchronization of edges and build geometry
            //if(prevListeners<maxListeners)
            stateManager.m_SyncEdgeBlocks = true;

            // Geometry needs to be rebuild
            stateManager.RequestState(ChunkState.BuildVertices);

            // Collider might beed to be rebuild
            if (chunk.NeedsCollider)
                chunk.Blocks.RequestCollider();
        }

        private void SubscribeNeighbors(bool subscribe)
        {
            Vector3Int pos = Chunk.Pos;
            SubscribeTwoNeighbors(new Vector3Int(pos.x + Env.CHUNK_SIZE, pos.y, pos.z), subscribe);
            SubscribeTwoNeighbors(new Vector3Int(pos.x - Env.CHUNK_SIZE, pos.y, pos.z), subscribe);
            SubscribeTwoNeighbors(new Vector3Int(pos.x, pos.y + Env.CHUNK_SIZE, pos.z), subscribe);
            SubscribeTwoNeighbors(new Vector3Int(pos.x, pos.y - Env.CHUNK_SIZE, pos.z), subscribe);
            SubscribeTwoNeighbors(new Vector3Int(pos.x, pos.y, pos.z + Env.CHUNK_SIZE), subscribe);
            SubscribeTwoNeighbors(new Vector3Int(pos.x, pos.y, pos.z - Env.CHUNK_SIZE), subscribe);

            // Update required listener count
            UpdateListenersCount(this);
        }

        private void SubscribeTwoNeighbors(Vector3Int neighborPos, bool subscribe)
        {
            World world = Chunk.World;

            // No chunk lookup if the neighbor positions can't be contained in the world
            //if (!world.CheckInsideWorld(neighborPos))
            //return;

            Chunk neighbor = world.Chunks.Get(ref neighborPos);
            if (neighbor == null)
                return;

            ChunkStateManagerClient stateManager = neighbor.StateManager;
            // Subscribe with each other. Passing Idle as event - it is ignored in this case anyway
            stateManager.Subscribe(this, ChunkState.Idle, subscribe);
            Subscribe(stateManager, ChunkState.Idle, subscribe);

            // Update required listener count of the neighbor
            UpdateListenersCount(stateManager);
        }
    }
}
