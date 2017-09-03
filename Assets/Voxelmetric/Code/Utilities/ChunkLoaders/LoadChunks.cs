using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.Math;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Core.Clipmap;
using Voxelmetric.Code.Core.StateManager;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Utilities.ChunkLoaders
{
    /// <summary>
    /// Running constantly, LoadChunks generates the world as we move.
    /// This script can be attached to any component. The world will be loaded based on its position
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LoadChunks : MonoBehaviour, IChunkLoader
    {
        private const int HORIZONTAL_MIN_RANGE = 0;
        private const int HORIZONTAL_MAX_RANGE = 32;
        private const int HORIZONTAL_DEF_RANGE = 6;
        private const int VERTICAL_MIN_RANGE = 0;
        private const int VERTICAL_MAX_RANGE = 32;
        private const int VERTICAL_DEF_RANGE = 3;

        //! The world we are attached to
        [SerializeField]
        private World m_World;
        public World World { get { return m_World; } set { m_World = value; } }
        //! The camera against which we perform frustrum checks
        private Camera m_Camera;
        //! Position of the camera when the game started
        private Vector3 m_CameraStartPos;

        //! Distance in chunks for loading chunks
        [Range(HORIZONTAL_MIN_RANGE, HORIZONTAL_MAX_RANGE)] public int HorizontalChunkLoadRadius = HORIZONTAL_DEF_RANGE;
        //! Distance in chunks for loading chunks
        [Range(VERTICAL_MIN_RANGE, VERTICAL_MAX_RANGE)] public int VerticalChunkLoadRadius = VERTICAL_DEF_RANGE;
        //! Makes the world regenerate around the attached camera. If false, X sticks at 0.
        [SerializeField]
        private bool m_FollowCameraX = true;
        public bool FollowCameraX { get { return m_FollowCameraX; } set { m_FollowCameraX = value; } }
        //! Makes the world regenerate around the attached camera. If false, Y sticks at 0.
        [SerializeField]
        private bool m_FollowCameraY = false;
        public bool FollowCameraY { get { return m_FollowCameraY; } set { m_FollowCameraY = value; } }
        //! Makes the world regenerate around the attached camera. If false, Z sticks at 0.
        [SerializeField]
        private bool m_FollowCameraZ = true;
        public bool FollowCameraZ { get { return m_FollowCameraZ; } set { m_FollowCameraZ = value; } }
        //! Toogles frustum culling
        [SerializeField]
        private bool m_UseFrustumCulling = true;
        public bool UseFrustumCulling { get { return m_UseFrustumCulling; } set { m_UseFrustumCulling = value; } }
        //! If false, only visible part of map is loaded on startup
        [SerializeField]
        private bool m_FullLoadOnStartUp = true;
        public bool FullLoadOnStartUp { get { return m_FullLoadOnStartUp; } set { m_FullLoadOnStartUp = value; } }

        public bool Diag_DrawWorldBounds = false;
        public bool Diag_DrawLoadRange = false;

        private int m_ChunkHorizontalLoadRadiusPrev;
        private int m_ChunkVerticalLoadRadiusPrev;

        private Vector3Int[] m_chunkPositions;
        private readonly Plane[] m_cameraPlanes = new Plane[6];
        private Clipmap m_clipmap;
        private Vector3Int m_viewerPos;
        private Vector3Int m_viewerPosPrev;

        //! A list of chunks to update
        private readonly List<Chunk> m_updateRequests = new List<Chunk>();

        void Awake()
        {
            Assert.IsNotNull(World);
            m_Camera = GetComponent<Camera>();
        }

        void Start()
        {
            m_ChunkHorizontalLoadRadiusPrev = HorizontalChunkLoadRadius;
            m_ChunkVerticalLoadRadiusPrev = VerticalChunkLoadRadius;

            m_CameraStartPos = m_Camera.transform.position;

            UpdateViewerPosition();

            // Add some arbirtary value so that m_viewerPosPrev is different from m_viewerPos
            m_viewerPosPrev += Vector3Int.one;
        }

        void Update()
        {
            Globals.GeometryBudget.Reset();
            Globals.EdgeSyncBudget.Reset();
            Globals.SetBlockBudget.Reset();

            PreProcessChunks();
            PostProcessChunks();
            ProcessChunks();
        }

        public void PreProcessChunks()
        {
            Profiler.BeginSample("PreProcessChunks");

            // Recalculate camera frustum planes
            Planes.CalculateFrustumPlanes(m_Camera, m_cameraPlanes);

            // Update clipmap based on range values
            UpdateRanges();

            // Update viewer position
            UpdateViewerPosition();

            // Update clipmap offsets based on the viewer position
            m_clipmap.SetOffset(
                m_viewerPos.x / Env.ChunkSize,
                m_viewerPos.y / Env.ChunkSize,
                m_viewerPos.z / Env.ChunkSize
                );


            Profiler.EndSample();
        }

        private void UpdateVisibility(int x, int y, int z, int rangeX, int rangeY, int rangeZ)
        {
            if (rangeX == 0 || rangeY == 0 || rangeZ == 0)
                return;

            bool isLast = rangeX == 1 && rangeY == 1 && rangeZ == 1;

            int wx = m_viewerPos.x + (x * Env.ChunkSize);
            int wy = m_viewerPos.y + (y * Env.ChunkSize);
            int wz = m_viewerPos.z + (z * Env.ChunkSize);

            int rx = rangeX * Env.ChunkSize;
            int ry = rangeY * Env.ChunkSize;
            int rz = rangeZ * Env.ChunkSize;

            // Stop if there is no further subdivision possible
            if (isLast)
            {
                // Update chunk's visibility information
                Vector3Int chunkPos = new Vector3Int(wx, wy, wz);
                Chunk chunk = World.Chunks.Get(ref chunkPos);
                if (chunk == null)
                    return;

                ChunkStateManagerClient stateManager = chunk.StateManager;

                int tx = m_clipmap.TransformX(x);
                int ty = m_clipmap.TransformY(y);
                int tz = m_clipmap.TransformZ(z);

                // Skip chunks which are too far away
                if (!m_clipmap.IsInsideBounds_Transformed(tx, ty, tz))
                    return;

                // Update visibility information
                ClipmapItem item = m_clipmap.Get_Transformed(tx, ty, tz);
                bool isVisible = Planes.TestPlanesAABB(m_cameraPlanes, chunk.WorldBounds);

                stateManager.Visible = isVisible && item.IsInVisibleRange;
                stateManager.PossiblyVisible = isVisible || FullLoadOnStartUp;

                return;
            }

            // Check whether the bouding box lies inside the camera's frustum
            AABB bounds2 = new AABB(wx, wy, wz, wx + rx, wy + ry, wz + rz);
            int inside = Planes.TestPlanesAABB2(m_cameraPlanes, bounds2);

            #region Full invisibility            

            if (inside == 0)
            {
                // Full invisibility. All chunks in this area need to be made invisible
                for (int cy = wy; cy < wy + ry; cy += Env.ChunkSize)
                {
                    for (int cz = wz; cz < wz + rz; cz += Env.ChunkSize)
                    {
                        for (int cx = wx; cx < wx + rx; cx += Env.ChunkSize)
                        {
                            // Update chunk's visibility information
                            Vector3Int chunkPos = new Vector3Int(cx, cy, cz);
                            Chunk chunk = World.Chunks.Get(ref chunkPos);
                            if (chunk == null)
                                continue;

                            ChunkStateManagerClient stateManager = chunk.StateManager;

                            // Update visibility information
                            stateManager.PossiblyVisible = FullLoadOnStartUp;
                            stateManager.Visible = false;
                        }
                    }
                }

                return;
            }

            #endregion

            #region Full visibility            

            if (inside == 6)
            {
                // Full visibility. All chunks in this area need to be made visible
                for (int cy = wy; cy < wy + ry; cy += Env.ChunkSize)
                {
                    for (int cz = wz; cz < wz + rz; cz += Env.ChunkSize)
                    {
                        for (int cx = wx; cx < wx + rx; cx += Env.ChunkSize)
                        {
                            // Update chunk's visibility information
                            Vector3Int chunkPos = new Vector3Int(cx, cy, cz);
                            Chunk chunk = World.Chunks.Get(ref chunkPos);
                            if (chunk == null)
                                continue;

                            ChunkStateManagerClient stateManager = chunk.StateManager;

                            int tx = m_clipmap.TransformX(x);
                            int ty = m_clipmap.TransformY(y);
                            int tz = m_clipmap.TransformZ(z);

                            // Update visibility information
                            ClipmapItem item = m_clipmap.Get_Transformed(tx, ty, tz);

                            stateManager.Visible = item.IsInVisibleRange;
                            stateManager.PossiblyVisible = true;
                        }
                    }
                }

                return;
            }

            #endregion

            #region Partial visibility

            int offX = rangeX;
            if (rangeX > 1)
            {
                offX = rangeX >> 1;
                rangeX = (rangeX + 1) >> 1; // ceil the number
            }
            int offY = rangeY;
            if (rangeY > 1)
            {
                offY = rangeY >> 1;
                rangeY = (rangeY + 1) >> 1; // ceil the number
            }
            int offZ = rangeZ;
            if (rangeZ > 1)
            {
                offZ = rangeZ >> 1;
                rangeZ = (rangeZ + 1) >> 1; // ceil the number
            }

            // Subdivide if possible
            // TODO: Avoid the recursion
            UpdateVisibility(x, y, z, offX, offY, offZ);
            UpdateVisibility(x + offX, y, z, rangeX, offY, offZ);
            UpdateVisibility(x, y, z + offZ, offX, offY, rangeZ);
            UpdateVisibility(x + offX, y, z + offZ, rangeX, offY, rangeZ);
            UpdateVisibility(x, y + offY, z, offX, rangeY, offZ);
            UpdateVisibility(x + offX, y + offY, z, rangeX, rangeY, offZ);
            UpdateVisibility(x, y + offY, z + offZ, offX, rangeY, rangeZ);
            UpdateVisibility(x + offX, y + offY, z + offZ, rangeX, rangeY, rangeZ);

            #endregion
        }

        private void HandleVisibility()
        {
            if (!UseFrustumCulling)
                return;

            Profiler.BeginSample("HandleVisibility");

            int minX = m_viewerPos.x - (HorizontalChunkLoadRadius * Env.ChunkSize);
            int maxX = m_viewerPos.x + (HorizontalChunkLoadRadius * Env.ChunkSize);
            int minY = m_viewerPos.y - (VerticalChunkLoadRadius * Env.ChunkSize);
            int maxY = m_viewerPos.y + (VerticalChunkLoadRadius * Env.ChunkSize);
            int minZ = m_viewerPos.z - (HorizontalChunkLoadRadius * Env.ChunkSize);
            int maxZ = m_viewerPos.z + (HorizontalChunkLoadRadius * Env.ChunkSize);
            World.CapCoordXInsideWorld(ref minX, ref maxX);
            World.CapCoordYInsideWorld(ref minY, ref maxY);
            World.CapCoordZInsideWorld(ref minZ, ref maxZ);

            minX /= Env.ChunkSize;
            maxX /= Env.ChunkSize;
            minY /= Env.ChunkSize;
            maxY /= Env.ChunkSize;
            minZ /= Env.ChunkSize;
            maxZ /= Env.ChunkSize;

            // TODO: Merge this with clipmap
            // Let's update chunk visibility info. Operate in chunk load radius so we know we're never outside cached range
            UpdateVisibility(minX, minY, minZ, maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);

            Profiler.EndSample();
        }

        public void PostProcessChunks()
        {
            int minX = m_viewerPos.x - (HorizontalChunkLoadRadius * Env.ChunkSize);
            int maxX = m_viewerPos.x + (HorizontalChunkLoadRadius * Env.ChunkSize);
            int minY = m_viewerPos.y - (VerticalChunkLoadRadius * Env.ChunkSize);
            int maxY = m_viewerPos.y + (VerticalChunkLoadRadius * Env.ChunkSize);
            int minZ = m_viewerPos.z - (HorizontalChunkLoadRadius * Env.ChunkSize);
            int maxZ = m_viewerPos.z + (HorizontalChunkLoadRadius * Env.ChunkSize);
            World.CapCoordXInsideWorld(ref minX, ref maxX);
            World.CapCoordYInsideWorld(ref minY, ref maxY);
            World.CapCoordZInsideWorld(ref minZ, ref maxZ);

            World.Bounds = new AABBInt(minX, minY, minZ, maxX, maxY, maxZ);

            int expectedChunks = m_chunkPositions.Length * ((maxY - minY + Env.ChunkSize) / Env.ChunkSize);

            if (// No update necessary if there was no movement
                m_viewerPos == m_viewerPosPrev &&
                // However, we need to make sure that we have enough chunks loaded
                World.Chunks.Count >= expectedChunks)
                return;

            // Unregister any non-necessary pending structures
            Profiler.BeginSample("UnregisterStructures");
            {
                World.UnregisterPendingStructures();
            }
            Profiler.EndSample();

            // Cycle through the array of positions
            Profiler.BeginSample("PostProcessChunks");
            {
                WorldChunks chunks = World.Chunks;

                // Cycle through the array of positions
                for (int y = maxY; y >= minY; y -= Env.ChunkSize)
                {
                    for (int i = 0; i < m_chunkPositions.Length; i++)
                    {
                        // Skip loading chunks which are off limits
                        int cx = (m_chunkPositions[i].x * Env.ChunkSize) + m_viewerPos.x;
                        if (cx > maxX || cx < minX)
                            continue;
                        int cy = (m_chunkPositions[i].y * Env.ChunkSize) + y;
                        if (cy > maxY || cy < minY)
                            continue;
                        int cz = (m_chunkPositions[i].z * Env.ChunkSize) + m_viewerPos.z;
                        if (cz > maxZ || cz < minZ)
                            continue;

                        // Create a new chunk if possible
                        Vector3Int newChunkPos = new Vector3Int(cx, cy, cz);
                        Chunk chunk;
                        if (!chunks.CreateOrGetChunk(ref newChunkPos, out chunk))
                            continue;

                        if (FullLoadOnStartUp)
                        {
                            ChunkStateManagerClient stateManager = chunk.StateManager;
                            stateManager.PossiblyVisible = true;
                            stateManager.Visible = false;
                        }

                        m_updateRequests.Add(chunk);
                    }
                }
            }
            Profiler.EndSample();
        }

        public void ProcessChunks()
        {
            Profiler.BeginSample("ProcessChunks");

            HandleVisibility();

            // Process removal requests
            for (int i = 0; i < m_updateRequests.Count;)
            {
                Chunk chunk = m_updateRequests[i];

                ProcessChunk(chunk);

                // Update the chunk if possible
                if (chunk.CanUpdate)
                {
                    chunk.UpdateState();

                    // Build colliders if there is enough time
                    if (Globals.GeometryBudget.HasTimeBudget)
                    {
                        Globals.GeometryBudget.StartMeasurement();

                        bool wasBuilt = chunk.UpdateRenderGeometry();
                        wasBuilt |= chunk.UpdateCollisionGeometry();
                        if (wasBuilt)
                            Globals.GeometryBudget.StopMeasurement();
                    }
                }

                // Automatically collect chunks which are ready to be removed from the world
                ChunkStateManagerClient stateManager = chunk.StateManager;
                if (stateManager.IsStateCompleted(ChunkState.Remove))
                {
                    // Remove the chunk from our provider and unregister it from chunk storage
                    World.Chunks.RemoveChunk(chunk);

                    // Unregister from updates
                    m_updateRequests.RemoveAt(i);
                    continue;
                }

                ++i;
            }

            World.PerformBlockActions();

            FullLoadOnStartUp = false;

            Profiler.EndSample();
        }

        public void ProcessChunk(Chunk chunk)
        {
            Profiler.BeginSample("ProcessChunk");

            ChunkStateManagerClient stateManager = chunk.StateManager;

            int tx = m_clipmap.TransformX(chunk.Pos.x / Env.ChunkSize);
            int ty = m_clipmap.TransformY(chunk.Pos.y / Env.ChunkSize);
            int tz = m_clipmap.TransformZ(chunk.Pos.z / Env.ChunkSize);

            // Chunk is too far away. Remove it
            if (!m_clipmap.IsInsideBounds_Transformed(tx, ty, tz))
            {
                stateManager.RequestState(ChunkState.Remove);
            }
            else
            {
                // Dummy collider example - create a collider for chunks directly surrounding the viewer
                int xd = Helpers.Abs((m_viewerPos.x - chunk.Pos.x) / Env.ChunkSize);
                int yd = Helpers.Abs((m_viewerPos.y - chunk.Pos.y) / Env.ChunkSize);
                int zd = Helpers.Abs((m_viewerPos.z - chunk.Pos.z) / Env.ChunkSize);
                chunk.NeedsCollider = xd <= 1 && yd <= 1 && zd <= 1;

                if (!UseFrustumCulling)
                {
                    ClipmapItem item = m_clipmap.Get_Transformed(tx, ty, tz);

                    // Chunk is in visibilty range. Full update with geometry generation is possible
                    if (item.IsInVisibleRange)
                    {
                        //chunk.LOD = item.LOD;
                        stateManager.PossiblyVisible = true;
                        stateManager.Visible = true;
                    }
                    // Chunk is in cached range. Full update except for geometry generation
                    else
                    {
                        //chunk.LOD = item.LOD;
                        stateManager.PossiblyVisible = true;
                        stateManager.Visible = false;
                    }
                }
            }
        }

        // Updates our clipmap region. Has to be set from the outside!
        private void UpdateRanges()
        {
            // Make sure horizontal ranges are always correct
            HorizontalChunkLoadRadius = Mathf.Max(HORIZONTAL_MIN_RANGE, HorizontalChunkLoadRadius);
            HorizontalChunkLoadRadius = Mathf.Min(HORIZONTAL_MAX_RANGE, HorizontalChunkLoadRadius);

            // Make sure vertical ranges are always correct
            VerticalChunkLoadRadius = Mathf.Max(VERTICAL_MIN_RANGE, VerticalChunkLoadRadius);
            VerticalChunkLoadRadius = Mathf.Min(VERTICAL_MAX_RANGE, VerticalChunkLoadRadius);

            bool isDifferenceXZ = HorizontalChunkLoadRadius != m_ChunkHorizontalLoadRadiusPrev || m_chunkPositions == null;
            bool isDifferenceY = VerticalChunkLoadRadius != m_ChunkVerticalLoadRadiusPrev;
            m_ChunkHorizontalLoadRadiusPrev = HorizontalChunkLoadRadius;
            m_ChunkVerticalLoadRadiusPrev = VerticalChunkLoadRadius;

            // Rebuild precomputed chunk positions
            if (isDifferenceXZ)
                m_chunkPositions = ChunkLoadOrder.ChunkPositions(HorizontalChunkLoadRadius);
            // Invalidate prev pos so that updated ranges can take effect right away
            if (isDifferenceXZ || isDifferenceY ||
                HorizontalChunkLoadRadius != m_ChunkHorizontalLoadRadiusPrev ||
                VerticalChunkLoadRadius != m_ChunkVerticalLoadRadiusPrev)
            {
                m_clipmap = new Clipmap(
                    HorizontalChunkLoadRadius,
                    VerticalChunkLoadRadius,
                    VerticalChunkLoadRadius + 1
                    );
                m_clipmap.Init(0, 0);

                m_viewerPos = m_viewerPos + Vector3Int.one; // Invalidate prev pos so that updated ranges can take effect right away
            }
        }

        private void UpdateViewerPosition()
        {
            Vector3Int chunkPos = transform.position.ToInt();
            Vector3Int pos = Chunk.ContainingChunkPos(ref chunkPos);

            // Update the viewer position
            m_viewerPosPrev = m_viewerPos;

            // Do not let y overflow
            int x = m_viewerPos.x;
            if (FollowCameraX)
            {
                x = pos.x;
                World.CapCoordXInsideWorld(ref x, ref x);
            }

            // Do not let y overflow
            int y = m_viewerPos.y;
            if (FollowCameraY)
            {
                y = pos.y;
                World.CapCoordYInsideWorld(ref y, ref y);
            }

            // Do not let y overflow
            int z = m_viewerPos.z;
            if (FollowCameraZ)
            {
                z = pos.z;
                World.CapCoordZInsideWorld(ref z, ref z);
            }

            m_viewerPos = new Vector3Int(x, y, z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled)
                return;

            float size = Env.ChunkSize * Env.BlockSize;
            float halfSize = size * 0.5f;
            float smallSize = size * 0.25f;

            if (World != null && World.Chunks != null && (Diag_DrawWorldBounds || Diag_DrawLoadRange))
            {
                foreach (Chunk chunk in World.Chunks.ChunkCollection)
                {
                    if (Diag_DrawWorldBounds)
                    {
                        // Make central chunks more apparent by using yellow color
                        bool isCentral = chunk.Pos.x == m_viewerPos.x || chunk.Pos.y == m_viewerPos.y || chunk.Pos.z == m_viewerPos.z;
                        Gizmos.color = isCentral ? Color.yellow : Color.blue;
                        Vector3 chunkCenter = new Vector3(
                            chunk.Pos.x + (Env.ChunkSize >> 1),
                            chunk.Pos.y + (Env.ChunkSize >> 1),
                            chunk.Pos.z + (Env.ChunkSize >> 1)
                            );
                        Vector3 chunkSize = new Vector3(Env.ChunkSize, Env.ChunkSize, Env.ChunkSize);
                        Gizmos.DrawWireCube(chunkCenter, chunkSize);
                    }

                    if (Diag_DrawLoadRange)
                    {
                        Vector3Int pos = chunk.Pos;

                        if (chunk.Pos.y == 0)
                        {
                            int tx = m_clipmap.TransformX(pos.x / Env.ChunkSize);
                            int ty = m_clipmap.TransformY(pos.y / Env.ChunkSize);
                            int tz = m_clipmap.TransformZ(pos.z / Env.ChunkSize);

                            if (!m_clipmap.IsInsideBounds_Transformed(tx, ty, tz))
                            {
                                Gizmos.color = Color.red;
                                Gizmos.DrawWireCube(
                                    new Vector3(pos.x + halfSize, 0, pos.z + halfSize),
                                    new Vector3(size - 1f, 0, size - 1f)
                                    );
                            }
                            else
                            {
                                ClipmapItem item = m_clipmap.Get_Transformed(tx, ty, tz);
                                if (item.IsInVisibleRange)
                                {
                                    Gizmos.color = Color.green;
                                    Gizmos.DrawWireCube(
                                        new Vector3(pos.x + halfSize, 0, pos.z + halfSize),
                                        new Vector3(size - 1f, 0, size - 1f)
                                        );
                                }
                            }
                        }

                        // Show generated chunks
                        ChunkStateManagerClient stateManager = chunk.StateManager;
                        if (stateManager.IsStateCompleted(ChunkState.Generate))
                        {
                            Gizmos.color = Color.magenta;
                            Gizmos.DrawWireCube(
                                new Vector3(pos.x + halfSize, pos.y + halfSize, pos.z + halfSize),
                                new Vector3(smallSize - 0.05f, smallSize - 0.05f, smallSize - 0.05f)
                                );
                        }
                    }
                }
            }
        }
    }
}
