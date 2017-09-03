using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using Voxelmetric.Code;
using Voxelmetric.Code.Builders.Geometry;
using Voxelmetric.Code.Common;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;
using Voxelmetric.Code.Geometry;
using Voxelmetric.Code.Load_Resources.Blocks;
using Voxelmetric.Code.Utilities.Import;

public class MagicaMeshBlockConfig : BlockConfig
{
    private RenderGeometryBuffer m_GeomBuffer = null;
    private Vector3 m_MeshOffset;
    private string m_Path;
    private float m_Scale;

    private int[] m_Triangles;
    private VertexData[] m_Vertices;

    public int[] Tris { get { return m_Triangles; } }

    public VertexData[] Verts { get { return m_Vertices; } }

    public override bool OnSetUp(Hashtable config, World world)
    {
        if (!base.OnSetUp(config, world))
            return false;

        Solid = _GetPropertyFromConfig(config, "solid", false);

        m_MeshOffset = new Vector3(
            Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshXOffset", "0"), CultureInfo.InvariantCulture),
            Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshYOffset", "0"), CultureInfo.InvariantCulture),
            Env.BlockSizeHalf + float.Parse(_GetPropertyFromConfig(config, "meshZOffset", "0"), CultureInfo.InvariantCulture)
        );
        m_Path = _GetPropertyFromConfig(config, "meshFileLocation", "");

        long scaleInv;
        if (!_GetPropertyFromConfig(config, "scaleInv", out scaleInv) || scaleInv <= 0)
            scaleInv = 1;
        m_Scale = 1f / scaleInv;

        return true;
    }

    public override bool OnPostSetUp(World world)
    {
        return SetUpMesh(world, world.Config.meshFolder + "/" + m_Path, m_MeshOffset, out m_Triangles, out m_Vertices);
    }

    protected bool SetUpMesh(World world, string meshLocation, Vector3 positionOffset, out int[] trisOut, out VertexData[] vertsOut)
    {
        trisOut = null;
        vertsOut = null;

        FileStream fs = null;
        try
        {
            string fullPath = Directories.ResourcesFolder + "/" + meshLocation + ".vox";
            fs = new FileStream(fullPath, FileMode.Open);
            using (BinaryReader br = new BinaryReader(fs))
            {
                // Load the magica vox model
                var data = MagicaVox.FromMagica(br);
                if (data == null)
                    return false;

                MagicaVox.MagicaVoxelChunk mvchunk = data.chunk;

                // Determine the biggest side
                int size = mvchunk.sizeX;
                if (mvchunk.sizeY > size)
                    size = mvchunk.sizeY;
                if (mvchunk.sizeZ > size)
                    size = mvchunk.sizeZ;

                // Determine the necessary size
                size += Env.ChunkPadding2;
                int pow = 1 + (int)Math.Log(size, 2);
                size = (1 << pow) - Env.ChunkPadding2;

                // Create a temporary chunk object
                Chunk chunk = new Chunk(size);
                chunk.Init(world, Vector3Int.zero);
                ChunkBlocks blocks = chunk.Blocks;

                // Convert the model's data to our internal system
                for (int y = 0; y < mvchunk.sizeY; y++)
                {
                    for (int z = 0; z < mvchunk.sizeZ; z++)
                    {
                        for (int x = 0; x < mvchunk.sizeX; x++)
                        {
                            int index = Helpers.GetChunkIndex1DFrom3D(x, y, z, pow);
                            int i = Helpers.GetIndex1DFrom3D(x, y, z, mvchunk.sizeX, mvchunk.sizeZ);

                            if (data.chunk.data[i] == 0)
                                blocks.SetInner(index, BlockProvider.airBlock);
                            else
                                blocks.SetInner(index, new BlockData(type));
                        }
                    }
                }

                Block block = world.BlockProvider.BlockTypes[type];

                block.Custom = false;
                m_GeomBuffer = new RenderGeometryBuffer();
                {
                    // Build the mesh
                    CubeMeshBuilder meshBuilder = new CubeMeshBuilder(m_Scale, size)
                    {
                        SideMask = 0
                    };
                    //meshBuilder.Palette = data.palette;
                    meshBuilder.Build(chunk);

                    // Convert lists to arrays
                    vertsOut = m_GeomBuffer.vertices.ToArray();
                    trisOut = m_GeomBuffer.triangles.ToArray();
                }
                m_GeomBuffer = null;
                block.Custom = true;

                fs = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
        finally
        {
            if (fs != null)
                fs.Dispose();
        }

        return true;
    }

    public bool IsInitPhase
    {
        get { return m_GeomBuffer != null; }
    }

    public void AddFace(VertexData[] vertexData, bool backFace)
    {
        Assert.IsTrue(vertexData.Length == 4);

        // Add data to the render buffer
        m_GeomBuffer.AddVertices(vertexData);
        m_GeomBuffer.AddIndices(m_GeomBuffer.vertices.Count, backFace);
    }
}
