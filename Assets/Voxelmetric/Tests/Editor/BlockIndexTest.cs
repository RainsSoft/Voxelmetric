using NUnit.Framework;
using Voxelmetric.Code;
using Voxelmetric.Code.Common;

public class BlockIndexTest
{
    [Test]
    public void ChunkIndexTest()
    {
        for (int y = 0; y <Env.CHUNK_SIZE; ++y)
            for (int z = 0; z <Env.CHUNK_SIZE; ++z)
                for (int x = 0; x<Env.CHUNK_SIZE; ++x)
                {
                    int index = Helpers.GetChunkIndex1DFrom3D(x, y, z);

                    int xx, yy, zz;
                    Helpers.GetChunkIndex3DFrom1D(index, out xx, out yy, out zz);

                    Assert.AreEqual(xx, x);
                    Assert.AreEqual(yy, y);
                    Assert.AreEqual(zz, z);
                }
    }

    [Test]
    public void IndexTest()
    {
        for (int y = 0; y < Env.CHUNK_SIZE; ++y)
            for (int z = 0; z < Env.CHUNK_SIZE; ++z)
                for (int x = 0; x < Env.CHUNK_SIZE; ++x)
                {
                    int index = Helpers.GetIndex1DFrom3D(
                        x+Env.CHUNK_PADDING,
                        y+Env.CHUNK_PADDING,
                        z+Env.CHUNK_PADDING,
                        Env.CHUNK_SIZE_WITH_PADDING,
                        Env.CHUNK_SIZE_WITH_PADDING
                        );

                    int xx, yy, zz;
                    Helpers.GetIndex3DFrom1D(index, out xx, out yy, out zz, Env.CHUNK_SIZE_WITH_PADDING, Env.CHUNK_SIZE_WITH_PADDING);
                    xx -= Env.CHUNK_PADDING;
                    yy -= Env.CHUNK_PADDING;
                    zz -= Env.CHUNK_PADDING;

                    Assert.AreEqual(xx, x);
                    Assert.AreEqual(yy, y);
                    Assert.AreEqual(zz, z);
                }
    }

	[Test]
    public void IndexIterationTest()
    {
        int index = Helpers.ZeroChunkIndex;
        int yOffset = Env.CHUNK_SIZE_WITH_PADDING_POW_2-Env.CHUNK_SIZE*Env.CHUNK_SIZE_WITH_PADDING;
        int zOffset = Env.CHUNK_SIZE_WITH_PADDING-Env.CHUNK_SIZE;

        for (int y = 0; y < Env.CHUNK_SIZE; ++y, index+=yOffset)
            for (int z = 0; z < Env.CHUNK_SIZE; ++z, index+=zOffset)
                for (int x = 0; x < Env.CHUNK_SIZE; ++x, ++index)
                {
                    int i = Helpers.GetChunkIndex1DFrom3D(x,y,z);
					Assert.AreEqual(index, i);
                }
    }
}
