using UnityEngine;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Configurable.Blocks
{
    public struct BlockFace
    {
        [System.Obsolete("Use '_block' instead.")]
        public Block block;
        public Dev_Block _block;
        public Vector3Int pos;
        public Direction side;
        public BlockLightData light;
        public int materialID;
    }
}
