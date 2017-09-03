using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Voxelmetric.Code.Common;

namespace Voxelmetric.Code.Utilities
{
    public static class ChunkLoadOrder
    {
        public static Vector3Int[] ChunkPositions(int chunkLoadRadius)
        {
            var chunkLoads = new List<Vector3Int>();
            for (int z = -chunkLoadRadius; z <= chunkLoadRadius; z++)
            {
                for (int x = -chunkLoadRadius; x <= chunkLoadRadius; x++)
                {
                    chunkLoads.Add(new Vector3Int(x, 0, z));
                }
            }

            //sort 2d vectors by closeness to center
            return chunkLoads
                .Where(pos => pos.x * pos.x + pos.z * pos.z <= chunkLoadRadius * chunkLoadRadius) // keep this a circle
                .OrderBy(pos => Helpers.Abs(pos.x) + Helpers.Abs(pos.z)) //smallest magnitude vectors first
                .ThenBy(pos => Helpers.Abs(pos.x)) //make sure not to process e.g (-10,0) before (5,5)
                .ThenBy(pos => Helpers.Abs(pos.z))
                .ToArray();
        }
    }
}
