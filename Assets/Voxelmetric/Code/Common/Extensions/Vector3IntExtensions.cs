using System;
using UnityEngine;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.Common.Extensions
{
    public static class Vector3IntExtensions
    {
        public static Vector3Int Add(this Vector3Int vector, int x, int y, int z)
        {
            return new Vector3Int(vector.x + x, vector.y + y, vector.z + z);
        }

        public static Vector3Int Add(this Vector3Int vector, Vector3Int pos)
        {
            return new Vector3Int(vector.x + pos.x, vector.y + pos.y, vector.z + pos.z);
        }

        public static Vector3Int Add(this Vector3Int vector, Direction direction)
        {
            Vector3Int toAdd = new Vector3Int(0, 0, 0);
            switch (direction)
            {
                case Direction.up:
                    toAdd.y = 1;
                    break;
                case Direction.down:
                    toAdd.y = -1;
                    break;
                case Direction.south:
                    toAdd.z = -1;
                    break;
                case Direction.north:
                    toAdd.z = 1;
                    break;
                case Direction.east:
                    toAdd.x = 1;
                    break;
                default: //West
                    toAdd.x = -1;
                    break;
            }

            return new Vector3Int(vector.x + toAdd.x, vector.y + toAdd.y, vector.z + toAdd.z);
        }

        public static Vector3Int Subtract(this Vector3Int vector, Vector3Int pos)
        {
            return new Vector3Int(vector.x - pos.x, vector.y - pos.y, vector.z - pos.z);
        }

        public static Vector3Int Negate(this Vector3Int vector)
        {
            return new Vector3Int(-vector.x, -vector.y, -vector.z);
        }

        public static byte[] ToBytes(this Vector3Int vector)
        {
            byte[] BX = BitConverter.GetBytes(vector.x);
            byte[] BY = BitConverter.GetBytes(vector.y);
            byte[] BZ = BitConverter.GetBytes(vector.z);

            return new[] { BX[0], BX[1], BX[2], BX[3], BY[0], BY[1], BY[2], BY[3], BZ[0], BZ[1], BZ[2], BZ[3] };
        }

        public static Vector3Int FromBytes(this Vector3Int vector, byte[] bytes, int offset)
        {
            return new Vector3Int(BitConverter.ToInt32(bytes, offset), BitConverter.ToInt32(bytes, offset + 4), BitConverter.ToInt32(bytes, offset + 8));
        }

        public static Vector3Int ToInt(this Vector3 vector)
        {
            return new Vector3Int(Mathf.RoundToInt(vector.x), Mathf.RoundToInt(vector.y), Mathf.RoundToInt(vector.z));
        }

        public static float Distance2(this Vector3Int vector, ref Vector3Int pos)
        {
            int xx = vector.x - pos.x;
            int yy = vector.y - pos.y;
            int zz = vector.z - pos.z;
            return xx * xx + yy * yy + zz * zz;
        }

        public static float Distance2(this Vector3Int vector, ref Vector3Int pos1, ref Vector3Int pos2)
        {
            int xx = pos1.x - pos2.x;
            int yy = pos1.y - pos2.y;
            int zz = pos1.z - pos2.z;
            return xx * xx + yy * yy + zz * zz;
        }
    }
}
