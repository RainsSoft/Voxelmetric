﻿using UnityEngine;
using System.Threading;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class Chunk : MonoBehaviour
{
    protected Block[,,] blocks = new Block[Config.Env.ChunkSize, Config.Env.ChunkSize, Config.Env.ChunkSize];

    protected List<BlockAndTimer> scheduledUpdates = new List<BlockAndTimer>();

    public enum Flag { busy, meshReady, loadStarted, generationInProgress, contentsGenerated, loadComplete, chunkModified, updateSoon, updateNow}
    public Hashtable flags = new Hashtable();

    protected MeshFilter filter;
    protected MeshCollider coll;

    public World world;
    public BlockPos pos;

    float randomUpdateTime = 0;

    protected MeshData meshData = new MeshData();
    public List<BlockPos> modifiedBlocks = new List<BlockPos>();

    protected virtual void Start()
    {
        filter = gameObject.GetComponent<MeshFilter>();
        coll = gameObject.GetComponent<MeshCollider>();

        gameObject.GetComponent<Renderer>().material.mainTexture = world.textureIndex.atlas;
    }

    protected virtual void LateUpdate()
    {
        TimedUpdated();

        if (GetFlag(Flag.updateNow))
        {
            UpdateChunk();
            SetFlag(Flag.updateNow, false);
            SetFlag(Flag.updateSoon, false);
        }

        if (GetFlag(Flag.meshReady))
        {
            SetFlag(Flag.meshReady, false);
            RenderMesh();
            meshData.ClearMesh();

            SetFlag(Flag.busy, false);
        }
    }

    public bool GetFlag(object key)
    {
        if (!flags.ContainsKey(key))
        {
            return false;
        }
        return (bool)flags[key];
    }

    public T GetFlag<T>(object key) where T : new()
    {
        if (!flags.ContainsKey(key))
        {
            return new T();
        }
        return (T)flags[key];
    }

    public void SetFlag(object key, object value)
    {
        if (flags.ContainsKey(key))
        {
            flags.Remove(key);
        }

        flags.Add(key, value);
    }

    protected virtual void TimedUpdated()
    {
        if (!GetFlag(Flag.loadComplete))
        {
            return;
        }

        randomUpdateTime += Time.fixedDeltaTime;
        if (randomUpdateTime >= world.config.randomUpdateFrequency)
        {
            randomUpdateTime = 0;

            BlockPos randomPos = pos;
            randomPos.x += Voxelmetric.resources.random.Next(0, 16);
            randomPos.y += Voxelmetric.resources.random.Next(0, 16);
            randomPos.z += Voxelmetric.resources.random.Next(0, 16);

            GetBlock(randomPos).RandomUpdate(this, randomPos, randomPos + pos);

            //Process Scheduled Updates
            for (int i = 0; i < scheduledUpdates.Count; i++)
            {
                scheduledUpdates[i] = new BlockAndTimer(scheduledUpdates[i].pos, scheduledUpdates[i].time - world.config.randomUpdateFrequency);
                if (scheduledUpdates[i].time <= 0)
                {
                    Block block = GetBlock(scheduledUpdates[i].pos);
                    block.ScheduledUpdate(this, scheduledUpdates[i].pos, scheduledUpdates[i].pos + pos);
                    scheduledUpdates.RemoveAt(i);
                    i--;
                }
            }

            if (GetFlag(Flag.updateSoon))
            {
                UpdateChunk();
                SetFlag(Flag.updateSoon, false);
                SetFlag(Flag.updateNow, false);
            }
        }

    }

    public void AddScheduledUpdate(BlockPos pos, float time)
    {
        scheduledUpdates.Add(new BlockAndTimer(pos, time));
    }

    /// <summary>
    /// Updates the chunk either now or as soon as the chunk is no longer busy
    /// </summary>
    public void UpdateNow()
    {
        SetFlag(Flag.updateNow, true);
    }

    public void UpdateSoon()
    {
        SetFlag(Flag.updateSoon, true);
    }

    /// <summary>
    /// Immediately updated the chunk and prepares a mesh to render. Usually better to use UpdateNow or UpdateSoon
    /// </summary>
    public virtual void UpdateChunk()
    {
        SetFlag(Flag.loadComplete, true);
        if (Config.Toggle.UseMultiThreading)
        {
            // If the chunk is busy set the flag to update it again
            // at the end of the the nearest frame 
            if (GetFlag(Flag.busy))
            {
                UpdateNow();
            }
            else
            {
                Thread thread = new Thread(() =>
                {
                    SetFlag(Flag.busy, true);
                    BuildMeshData();
                    SetFlag(Flag.meshReady, true);
                });
                thread.Start();
            }
        }
        else //Not using multithreading
        {
            SetFlag(Flag.busy, true);
            BuildMeshData();
            SetFlag(Flag.meshReady, true);
        }
    }

    /// <summary>
    /// Gets and returns a block from a position within the chunk 
    /// or fetches it from the world
    /// </summary>
    /// <param name="blockPos">A global block position</param>
    /// <returns>The block at the position</returns>
    public virtual Block GetBlock(BlockPos blockPos)
    {
        if (InRange(blockPos))
        {
            return LocalGetBlock(blockPos - pos);
        }
        else
        {
            return world.GetBlock(blockPos);
        }
    }

    /// <summary>
    /// This function takes a block position relative to the chunk's position. It is slightly faster
    /// than the GetBlock function so use this if you already have a local position available otherwise
    /// use GetBlock. If the position is lesser or greater than the size of the chunk it will get the value
    /// from the chunk containing the block pos
    /// </summary>
    /// <param name="localBlockPos"> A block pos relative to the chunk's position. MUST be a local position or the wrong block will be returned</param>
    /// <returns>the block at the relative position</returns>
    public virtual Block LocalGetBlock(BlockPos localBlockPos)
    {
        if ((localBlockPos.x < Config.Env.ChunkSize && localBlockPos.x >= 0) &&
            (localBlockPos.y < Config.Env.ChunkSize && localBlockPos.y >= 0) &&
            (localBlockPos.z < Config.Env.ChunkSize && localBlockPos.z >= 0))
        {
            Block block = blocks[localBlockPos.x, localBlockPos.y, localBlockPos.z];
            if (block == null)
            {
                return Block.Air;
            }
            else
            {
                return block;
            }
        }
        else
        {
            return world.GetBlock(localBlockPos + pos);
        }
    }

    /// <summary>
    /// This function takes a block position relative to the chunk's position. It is slightly faster
    /// than the SetBlock function so use this if you already have a local position available otherwise
    /// use SetBlock. If the position is lesser or greater than the size of the chunk it will call setblock
    /// using the world.
    /// </summary>
    /// <param name="blockPos"> A block pos relative to the chunk's position.</param>
    public virtual void LocalSetBlock(BlockPos blockPos, Block block)
    {
        if ((blockPos.x < Config.Env.ChunkSize && blockPos.x >= 0) &&
            (blockPos.y < Config.Env.ChunkSize && blockPos.y >= 0) &&
            (blockPos.z < Config.Env.ChunkSize && blockPos.z >= 0))
        {
            blocks[blockPos.x, blockPos.y, blockPos.z] = block;
        }
    }

    /// <summary>
    /// Returns true if the block local block position is contained in the chunk boundaries
    /// </summary>
    /// <param name="blockPos">A block position</param>
    public bool InRange(BlockPos blockPos)
    {
        return (blockPos.ContainingChunkCoordinates() == pos);
    }

    public virtual void SetBlock(BlockPos blockPos, String block, bool updateChunk = true, bool setBlockModified = true)
    {
        SetBlock(blockPos, Block.New(block, world), updateChunk, setBlockModified);
    }

    /// <summary>
    /// Sets the block at the given position
    /// </summary>
    /// <param name="blockPos">Block position</param>
    /// <param name="block">Block to place at the given location</param>
    /// <param name="updateChunk">Optional parameter, set to false to keep the chunk unupdated despite the change</param>
    public virtual void SetBlock(BlockPos blockPos, Block block, bool updateChunk = true, bool setBlockModified = true)
    {
        if (InRange(blockPos))
        {
            //Only call create and destroy if this is a different block type, otherwise it's just updating the properties of an existing block
            if (GetBlock(blockPos).type != block.type)
            {
                GetBlock(blockPos).OnDestroy(this, blockPos, blockPos + pos);
                block.OnCreate(this, blockPos, blockPos + pos);
            }

            blocks[blockPos.x - pos.x, blockPos.y - pos.y, blockPos.z - pos.z] = block;

            if (setBlockModified)
                SetBlockModified(blockPos);

            if (updateChunk)
                UpdateNow();
        }
        else
        {
            //if the block is out of range set it through world
            world.SetBlock(blockPos, block, updateChunk);
        }
    }

    /// <summary>
    /// Updates the chunk based on its contents
    /// </summary>
    protected virtual void BuildMeshData()
    {
        for (int x = 0; x < Config.Env.ChunkSize; x++)
        {
            for (int y = 0; y < Config.Env.ChunkSize; y++)
            {
                for (int z = 0; z < Config.Env.ChunkSize; z++)
                {
                    BlockPos localBlockPos = new BlockPos(x, y, z);
                    if (LocalGetBlock(localBlockPos).type != 0)
                    {
                        LocalGetBlock(localBlockPos).BuildBlock(this, localBlockPos, localBlockPos + pos, meshData);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sends the calculated mesh information
    /// to the mesh and collision components
    /// </summary>
    protected virtual void RenderMesh()
    {
        filter.mesh.Clear();
        filter.mesh.vertices = meshData.vertices.ToArray();
        filter.mesh.triangles = meshData.triangles.ToArray();

        filter.mesh.colors = meshData.colors.ToArray();

        filter.mesh.uv = meshData.uv.ToArray();
        filter.mesh.RecalculateNormals();

        if (world.config.useCollisionMesh)
        {
            coll.sharedMesh = null;
            Mesh mesh = new Mesh();
            mesh.vertices = meshData.colVertices.ToArray();
            mesh.triangles = meshData.colTriangles.ToArray();
            mesh.RecalculateNormals();

            coll.sharedMesh = mesh;
        }
    }

    public void ReturnChunkToPool()
    {
        flags.Clear();

        if (filter.mesh)
            filter.mesh.Clear();

        if (coll.sharedMesh)
            coll.sharedMesh.Clear();

        blocks = new Block[Config.Env.ChunkSize, Config.Env.ChunkSize, Config.Env.ChunkSize];
        meshData = new MeshData();

        world.chunks.Remove(pos);
        world.AddToChunkPool(gameObject);
    }

    public void SetBlockModified(BlockPos pos)
    {
        if (!modifiedBlocks.Contains(pos))
        {
            modifiedBlocks.Add(pos);
            SetFlag(Flag.chunkModified, true);
        }
    }
}