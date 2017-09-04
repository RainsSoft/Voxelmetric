using NUnit.Framework;
using UnityEngine;
using Voxelmetric.Code;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

public class BlockPosTest {
    [Test]
    public void ContainingChunkCoordinatesTest() {
        for(int x=-Env.CHUNK_SIZE; x<=2*Env.CHUNK_SIZE; x++)
            for (int y = -Env.CHUNK_SIZE; y <= 2 * Env.CHUNK_SIZE; y++)
                for (int z = -Env.CHUNK_SIZE; z <= 2 * Env.CHUNK_SIZE; z++)
                    AssertContainingChunkCoordinates(new Vector3Int(x, y, z));
    }

    private static void AssertContainingChunkCoordinates(Vector3Int pos) {
        Assert.AreEqual(ExpContainingChunkCoordinates(pos), Chunk.ContainingChunkPos(ref pos), pos.ToString());
    }

    //returns the position of the chunk containing this block
    private static Vector3Int ExpContainingChunkCoordinates(Vector3Int pos) {
        int chunkSize = Env.CHUNK_SIZE;

        int cx = Mathf.FloorToInt(pos.x / (float)chunkSize) * chunkSize;
        int cy = Mathf.FloorToInt(pos.y / (float)chunkSize) * chunkSize;
        int cz = Mathf.FloorToInt(pos.z / (float)chunkSize) * chunkSize;

        return new Vector3Int(cx, cy, cz);
    }
}
