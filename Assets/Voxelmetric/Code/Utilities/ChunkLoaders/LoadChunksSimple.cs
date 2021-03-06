using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Common.Math;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Core.StateManager;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Utilities.ChunkLoaders
{
    /// <summary>
    /// Running constantly, LoadChunks generates the world as we move.
    /// This script can be attached to any component. The world will be loaded based on its position
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class LoadChunksSimple : MonoBehaviour, IChunkLoader
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

        //! Distance in chunks for loading chunks
        [Range(HORIZONTAL_MIN_RANGE, HORIZONTAL_MAX_RANGE)]
        [SerializeField]
        private int m_HorizontalChunkLoadRadius = HORIZONTAL_DEF_RANGE;
        public int HorizontalChunkLoadRadius { get { return m_HorizontalChunkLoadRadius; } set { m_HorizontalChunkLoadRadius = value; } }
        //! Distance in chunks for loading chunks
        [Range(VERTICAL_MIN_RANGE, VERTICAL_MAX_RANGE)]
        [SerializeField]
        private int m_VerticalChunkLoadRadius = VERTICAL_DEF_RANGE;
        public int VerticalChunkLoadRadius { get { return m_VerticalChunkLoadRadius; } set { m_VerticalChunkLoadRadius = value; } }
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

        [SerializeField]
        private bool m_DrawWorldBounds = false;
        public bool DrawWorldBounds { get { return m_DrawWorldBounds; } set { m_DrawWorldBounds = value; } }
        [SerializeField]
        private bool m_DrawLoadRange = false;
        public bool DrawLoadRange { get { return m_DrawLoadRange; } set { m_DrawLoadRange = value; } }

        private int m_ChunkHorizontalLoadRadiusPrev;
        private int m_ChunkVerticalLoadRadiusPrev;

        private Vector3Int[] m_ChunkPositions;
        private readonly Plane[] cameraPlanes = new Plane[6];
        private Vector3Int m_ViewerPos;
        private Vector3Int m_ViewerPosPrev;

        //! A list of chunks to update
        private readonly List<Chunk> updateRequests = new List<Chunk>();

        void Awake()
        {
            Assert.IsNotNull(World);
            m_Camera = GetComponent<Camera>();
        }

        void Start()
        {
            m_ChunkHorizontalLoadRadiusPrev = m_HorizontalChunkLoadRadius;
            m_ChunkVerticalLoadRadiusPrev = m_VerticalChunkLoadRadius;

            UpdateViewerPosition();

            // Add some arbirtary value so that m_viewerPosPrev is different from m_viewerPos
            m_ViewerPosPrev += Vector3Int.one;
        }

        void Update()
        {
            Globals.GeometryBudget.Reset();
            Globals.EdgeSyncBudget.Reset();

            PreProcessChunks();
            PostProcessChunks();
            ProcessChunks();
        }

        public void PreProcessChunks()
        {
            Profiler.BeginSample("PreProcessChunks");

            // Recalculate camera frustum planes
            Planes.CalculateFrustumPlanes(m_Camera, cameraPlanes);

            // Update clipmap based on range values
            UpdateRanges();

            // Update viewer position
            UpdateViewerPosition();

            Profiler.EndSample();
        }

        private void UpdateVisibility(int x, int y, int z, int rangeX, int rangeY, int rangeZ)
        {
            if (rangeX == 0 || rangeY == 0 || rangeZ == 0)
                return;

            bool isLast = rangeX == 1 && rangeY == 1 && rangeZ == 1;

            int wx = x * Env.CHUNK_SIZE;
            int wy = y * Env.CHUNK_SIZE;
            int wz = z * Env.CHUNK_SIZE;

            // Stop if there is no further subdivision possible
            if (isLast)
            {
                // Update chunk's visibility information
                Vector3Int chunkPos = new Vector3Int(wx, wy, wz);
                Chunk chunk = World.Chunks.Get(ref chunkPos);
                if (chunk == null)
                    return;

                ChunkStateManagerClient stateManager = chunk.StateManager;

                int xd = (m_ViewerPos.x - chunk.Pos.x) / Env.CHUNK_SIZE;
                int yd = (m_ViewerPos.y - chunk.Pos.y) / Env.CHUNK_SIZE;
                int zd = (m_ViewerPos.z - chunk.Pos.z) / Env.CHUNK_SIZE;

                int xDist = xd * xd + zd * zd;
                int yDist = yd * yd;

                // Update visibility information
                bool isVisible = Planes.TestPlanesAABB(cameraPlanes, chunk.WorldBounds);

                stateManager.Visible = isVisible &&
                                        xDist <= m_HorizontalChunkLoadRadius * m_HorizontalChunkLoadRadius &&
                                        yDist <= m_VerticalChunkLoadRadius * m_VerticalChunkLoadRadius;
                stateManager.PossiblyVisible = isVisible || m_FullLoadOnStartUp;

                return;
            }

            int rx = rangeX * Env.CHUNK_SIZE;
            int ry = rangeY * Env.CHUNK_SIZE;
            int rz = rangeZ * Env.CHUNK_SIZE;

            /*AABB bounds2 = new AABB(wx, wy, wz, wx+rx, wy+ry, wz+rz);
            int inside = 0;

            // if the camera position lies inside the bounding box we'll assume partial visibility automatically
            if (bounds2.IsInside(m_camera.transform.position))
                inside = 3;
            else
            {
                // Check whether the bouding box lies inside the camera's frustum
                inside = Geometry.TestPlanesAABB2(m_cameraPlanes, bounds2);
            }*/
            // Check whether the bouding box lies inside the camera's frustum
            AABB bounds2 = new AABB(wx, wy, wz, wx + rx, wy + ry, wz + rz);
            int inside = Planes.TestPlanesAABB2(cameraPlanes, bounds2);

            #region Full invisibility            

            if (inside == 0)
            {
                // Full invisibility. All chunks in this area need to be made invisible
                for (int cy = wy; cy < wy + ry; cy += Env.CHUNK_SIZE)
                {
                    for (int cz = wz; cz < wz + rz; cz += Env.CHUNK_SIZE)
                    {
                        for (int cx = wx; cx < wx + rx; cx += Env.CHUNK_SIZE)
                        {
                            // Update chunk's visibility information
                            Vector3Int chunkPos = new Vector3Int(cx, cy, cz);
                            Chunk chunk = World.Chunks.Get(ref chunkPos);
                            if (chunk == null)
                                continue;

                            ChunkStateManagerClient stateManager = chunk.StateManager;

                            // Update visibility information
                            stateManager.PossiblyVisible = m_FullLoadOnStartUp;
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
                for (int cy = wy; cy < wy + ry; cy += Env.CHUNK_SIZE)
                {
                    for (int cz = wz; cz < wz + rz; cz += Env.CHUNK_SIZE)
                    {
                        for (int cx = wx; cx < wx + rx; cx += Env.CHUNK_SIZE)
                        {
                            // Update chunk's visibility information
                            Vector3Int chunkPos = new Vector3Int(cx, cy, cz);
                            Chunk chunk = World.Chunks.Get(ref chunkPos);
                            if (chunk == null)
                                continue;

                            ChunkStateManagerClient stateManager = chunk.StateManager;

                            int xd = (m_ViewerPos.x - chunk.Pos.x) / Env.CHUNK_SIZE;
                            int yd = (m_ViewerPos.y - chunk.Pos.y) / Env.CHUNK_SIZE;
                            int zd = (m_ViewerPos.z - chunk.Pos.z) / Env.CHUNK_SIZE;

                            int xDist = xd * xd + zd * zd;
                            int yDist = yd * yd;

                            // Update visibility information
                            stateManager.Visible = xDist <= m_HorizontalChunkLoadRadius * m_HorizontalChunkLoadRadius &&
                                                   yDist <= m_VerticalChunkLoadRadius * m_VerticalChunkLoadRadius;
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
            if (!m_UseFrustumCulling)
                return;

            Profiler.BeginSample("HandleVisibility");

            int minX = m_ViewerPos.x - (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxX = m_ViewerPos.x + (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int minY = m_ViewerPos.y - (m_VerticalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxY = m_ViewerPos.y + (m_VerticalChunkLoadRadius * Env.CHUNK_SIZE);
            int minZ = m_ViewerPos.z - (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxZ = m_ViewerPos.z + (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            World.CapCoordXInsideWorld(ref minX, ref maxX);
            World.CapCoordYInsideWorld(ref minY, ref maxY);
            World.CapCoordZInsideWorld(ref minZ, ref maxZ);

            minX /= Env.CHUNK_SIZE;
            maxX /= Env.CHUNK_SIZE;
            minY /= Env.CHUNK_SIZE;
            maxY /= Env.CHUNK_SIZE;
            minZ /= Env.CHUNK_SIZE;
            maxZ /= Env.CHUNK_SIZE;

            // TODO: Merge this with clipmap
            // Let's update chunk visibility info. Operate in chunk load radius so we know we're never outside cached range
            UpdateVisibility(minX, minY, minZ, maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1);

            Profiler.EndSample();
        }

        public void PostProcessChunks()
        {
            int minX = m_ViewerPos.x - (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxX = m_ViewerPos.x + (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int minY = m_ViewerPos.y - (m_VerticalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxY = m_ViewerPos.y + (m_VerticalChunkLoadRadius * Env.CHUNK_SIZE);
            int minZ = m_ViewerPos.z - (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            int maxZ = m_ViewerPos.z + (m_HorizontalChunkLoadRadius * Env.CHUNK_SIZE);
            World.CapCoordXInsideWorld(ref minX, ref maxX);
            World.CapCoordYInsideWorld(ref minY, ref maxY);
            World.CapCoordZInsideWorld(ref minZ, ref maxZ);

            World.Bounds = new AABBInt(minX, minY, minZ, maxX, maxY, maxZ);

            int expectedChunks = m_ChunkPositions.Length * ((maxY - minY + Env.CHUNK_SIZE) / Env.CHUNK_SIZE);

            if (// No update necessary if there was no movement
                m_ViewerPos == m_ViewerPosPrev &&
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
                for (int y = maxY; y >= minY; y -= Env.CHUNK_SIZE)
                {
                    for (int i = 0; i < m_ChunkPositions.Length; i++)
                    {
                        // Skip loading chunks which are off limits
                        int cx = (m_ChunkPositions[i].x * Env.CHUNK_SIZE) + m_ViewerPos.x;
                        if (cx > maxX || cx < minX)
                            continue;
                        int cy = (m_ChunkPositions[i].y * Env.CHUNK_SIZE) + y;
                        if (cy > maxY || cy < minY)
                            continue;
                        int cz = (m_ChunkPositions[i].z * Env.CHUNK_SIZE) + m_ViewerPos.z;
                        if (cz > maxZ || cz < minZ)
                            continue;

                        // Create a new chunk if possible
                        Vector3Int newChunkPos = new Vector3Int(cx, cy, cz);
                        Chunk chunk;
                        if (!chunks.CreateOrGetChunk(ref newChunkPos, out chunk))
                            continue;

                        if (m_FullLoadOnStartUp)
                        {
                            ChunkStateManagerClient stateManager = chunk.StateManager;
                            stateManager.PossiblyVisible = true;
                            stateManager.Visible = false;
                        }

                        updateRequests.Add(chunk);
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
            for (int i = 0; i < updateRequests.Count;)
            {
                Chunk chunk = updateRequests[i];

                ProcessChunk(chunk);

                // Update the chunk if possible
                if (chunk.CanUpdate)
                {
                    chunk.UpdateState();
                    chunk.UpdateRenderGeometry();
                    chunk.UpdateCollisionGeometry();
                }

                // Automatically collect chunks which are ready to be removed from the world
                ChunkStateManagerClient stateManager = chunk.StateManager;
                if (stateManager.IsStateCompleted(ChunkState.Remove))
                {
                    // Remove the chunk from our provider and unregister it from chunk storage
                    World.Chunks.RemoveChunk(chunk);

                    // Unregister from updates
                    updateRequests.RemoveAt(i);

                    continue;
                }

                ++i;
            }

            World.PerformBlockActions();

            m_FullLoadOnStartUp = false;

            Profiler.EndSample();

            // Memory savings diagnostic
            /*{
                long compressedMem = 0;
                foreach (var chunk in world.chunks.chunkCollection)
                {
                    compressedMem += chunk.blocks.BlocksCompressed.Count*
                                     StructSerialization.TSSize<BlockDataAABB>.ValueSize;
                }

                long uncompressedMem = (long)world.chunks.Count*Env.ChunkSizeWithPaddingPow3*
                                       StructSerialization.TSSize<BlockData>.ValueSize;
                Debug.LogFormat("mem: {0}/{1} ({2}%)", compressedMem, uncompressedMem, (int)((double)compressedMem/(double)uncompressedMem*100.0));
            }*/
        }

        public void ProcessChunk(Chunk chunk)
        {
            Profiler.BeginSample("ProcessChunk");

            ChunkStateManagerClient stateManager = chunk.StateManager;

            int xd = (m_ViewerPos.x - chunk.Pos.x) / Env.CHUNK_SIZE;
            int yd = (m_ViewerPos.y - chunk.Pos.y) / Env.CHUNK_SIZE;
            int zd = (m_ViewerPos.z - chunk.Pos.z) / Env.CHUNK_SIZE;

            int hRadius = m_HorizontalChunkLoadRadius;
            int vRadius = m_VerticalChunkLoadRadius;
            int xDist = xd * xd + zd * zd;
            int yDist = yd * yd;

            // Remove the chunk if it is too far away
            if (xDist > hRadius * hRadius || yDist > vRadius * vRadius)
            {
                stateManager.RequestState(ChunkState.Remove);
            }
            else
            {
                // Dummy collider example - create a collider for chunks directly surrounding the viewer
                chunk.NeedsCollider = Helpers.Abs(xd) <= 1 && Helpers.Abs(yd) <= 1 && Helpers.Abs(zd) <= 1;

                if (!m_UseFrustumCulling)
                {
                    // Update visibility information
                    stateManager.Visible = xDist <= m_HorizontalChunkLoadRadius * m_HorizontalChunkLoadRadius &&
                                           yDist <= m_VerticalChunkLoadRadius * m_VerticalChunkLoadRadius;
                    stateManager.PossiblyVisible = true;
                }
            }

            Profiler.EndSample();
        }

        private void UpdateRanges()
        {
            // Make sure horizontal ranges are always correct
            m_HorizontalChunkLoadRadius = Mathf.Max(HORIZONTAL_MIN_RANGE, m_HorizontalChunkLoadRadius);
            m_HorizontalChunkLoadRadius = Mathf.Min(HORIZONTAL_MAX_RANGE, m_HorizontalChunkLoadRadius);

            // Make sure vertical ranges are always correct
            m_VerticalChunkLoadRadius = Mathf.Max(VERTICAL_MIN_RANGE, m_VerticalChunkLoadRadius);
            m_VerticalChunkLoadRadius = Mathf.Min(VERTICAL_MAX_RANGE, m_VerticalChunkLoadRadius);

            bool isDifferenceXZ = m_HorizontalChunkLoadRadius != m_ChunkHorizontalLoadRadiusPrev || m_ChunkPositions == null;
            bool isDifferenceY = m_VerticalChunkLoadRadius != m_ChunkVerticalLoadRadiusPrev;
            m_ChunkHorizontalLoadRadiusPrev = m_HorizontalChunkLoadRadius;
            m_ChunkVerticalLoadRadiusPrev = m_VerticalChunkLoadRadius;

            // Rebuild precomputed chunk positions
            if (isDifferenceXZ)
                m_ChunkPositions = ChunkLoadOrder.ChunkPositions(m_HorizontalChunkLoadRadius);
            // Invalidate prev pos so that updated ranges can take effect right away
            if (isDifferenceXZ || isDifferenceY ||
                m_HorizontalChunkLoadRadius != m_ChunkHorizontalLoadRadiusPrev ||
                m_VerticalChunkLoadRadius != m_ChunkVerticalLoadRadiusPrev)
                m_ViewerPos = m_ViewerPos + Vector3Int.one;
        }

        private void UpdateViewerPosition()
        {
            Vector3Int chunkPos = transform.position.ToInt();
            Vector3Int pos = Chunk.ContainingChunkPos(ref chunkPos);

            // Update the viewer position
            m_ViewerPosPrev = m_ViewerPos;

            // Do not let y overflow
            int x = m_ViewerPos.x;
            if (FollowCameraX)
            {
                x = pos.x;
                World.CapCoordXInsideWorld(ref x, ref x);
            }

            // Do not let y overflow
            int y = m_ViewerPos.y;
            if (FollowCameraY)
            {
                y = pos.y;
                World.CapCoordYInsideWorld(ref y, ref y);
            }

            // Do not let y overflow
            int z = m_ViewerPos.z;
            if (FollowCameraZ)
            {
                z = pos.z;
                World.CapCoordZInsideWorld(ref z, ref z);
            }

            m_ViewerPos = new Vector3Int(x, y, z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!enabled)
                return;

            float size = Env.CHUNK_SIZE * Env.BLOCK_SIZE;
            float halfSize = size * 0.5f;
            float smallSize = size * 0.25f;

            if (World != null && World.Chunks != null && (DrawWorldBounds || DrawLoadRange))
            {
                foreach (Chunk chunk in World.Chunks.ChunkCollection)
                {
                    if (DrawWorldBounds)
                    {
                        // Make central chunks more apparent by using yellow color
                        bool isCentral = chunk.Pos.x == m_ViewerPos.x || chunk.Pos.y == m_ViewerPos.y || chunk.Pos.z == m_ViewerPos.z;
                        Gizmos.color = isCentral ? Color.yellow : Color.blue;
                        Vector3 chunkCenter = new Vector3(
                            chunk.Pos.x + (Env.CHUNK_SIZE >> 1),
                            chunk.Pos.y + (Env.CHUNK_SIZE >> 1),
                            chunk.Pos.z + (Env.CHUNK_SIZE >> 1)
                            );
                        Vector3 chunkSize = new Vector3(Env.CHUNK_SIZE, Env.CHUNK_SIZE, Env.CHUNK_SIZE);
                        Gizmos.DrawWireCube(chunkCenter, chunkSize);
                    }

                    if (DrawLoadRange)
                    {
                        Vector3Int pos = chunk.Pos;
                        int xd = Helpers.Abs((m_ViewerPos.x - pos.x) / Env.CHUNK_SIZE);
                        int zd = Helpers.Abs((m_ViewerPos.z - pos.z) / Env.CHUNK_SIZE);
                        int dist = xd * xd + zd * zd;
                        if (dist <= m_HorizontalChunkLoadRadius * m_HorizontalChunkLoadRadius)
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawWireCube(
                                new Vector3(chunk.Pos.x + halfSize, 0, chunk.Pos.z + halfSize),
                                new Vector3(size - 1f, 0, size - 1f)
                                );
                        }
                        else
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireCube(
                                new Vector3(chunk.Pos.x + halfSize, 0, chunk.Pos.z + halfSize),
                                new Vector3(size - 1f, 0, size - 1f)
                                );
                        }

                        // Show generated chunks
                        ChunkStateManagerClient stateManager = chunk.StateManager;
                        if (stateManager.IsStateCompleted(ChunkState.Generate))
                        {
                            Gizmos.color = Color.magenta;
                            Gizmos.DrawWireCube(
                                new Vector3(chunk.Pos.x + halfSize, 0, chunk.Pos.z + halfSize),
                                new Vector3(smallSize - 0.05f, 0, smallSize - 0.05f)
                                );
                        }
                    }
                }
            }
        }
    }
}
