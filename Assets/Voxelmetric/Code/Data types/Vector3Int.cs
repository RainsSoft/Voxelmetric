using System;
using UnityEngine;

namespace Voxelmetric.Code.Data_types
{
    [Serializable]
    public struct Old_Vector3Int : IEquatable<Old_Vector3Int>
    {
        public static readonly Old_Vector3Int zero = new Old_Vector3Int(0, 0, 0);
        public static readonly Old_Vector3Int one = new Old_Vector3Int(1, 1, 1);
        public static readonly Old_Vector3Int up = new Old_Vector3Int(0, 1, 0);
        public static readonly Old_Vector3Int down = new Old_Vector3Int(0, -1, 0);
        public static readonly Old_Vector3Int north = new Old_Vector3Int(0, 0, 1);
        public static readonly Old_Vector3Int south = new Old_Vector3Int(0, 0, -1);
        public static readonly Old_Vector3Int east = new Old_Vector3Int(1, 0, 0);
        public static readonly Old_Vector3Int west = new Old_Vector3Int(-1, 0, 0);

        public readonly int x, y, z;

        public Old_Vector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Old_Vector3Int Add(int x, int y, int z)
        {
            return new Old_Vector3Int(this.x + x, this.y + y, this.z + z);
        }

        public Old_Vector3Int Add(Old_Vector3Int pos)
        {
            return new Old_Vector3Int(x + pos.x, y + pos.y, z + pos.z);
        }

        public Old_Vector3Int Subtract(Old_Vector3Int pos)
        {
            return new Old_Vector3Int(x - pos.x, y - pos.y, z - pos.z);
        }

        public Old_Vector3Int Negate()
        {
            return new Old_Vector3Int(-x, -y, -z);
        }

        public byte[] ToBytes()
        {
            byte[] BX = BitConverter.GetBytes(x);
            byte[] BY = BitConverter.GetBytes(y);
            byte[] BZ = BitConverter.GetBytes(z);

            return new[] {
                BX[0], BX[1], BX[2], BX[3],
                BY[0], BY[1], BY[2], BY[3],
                BZ[0], BZ[1], BZ[2], BZ[3]};
        }

        public static Old_Vector3Int FromBytes(byte[] bytes, int offset)
        {
            return new Old_Vector3Int(
                BitConverter.ToInt32(bytes, offset),
                BitConverter.ToInt32(bytes, offset + 4),
                BitConverter.ToInt32(bytes, offset + 8));
        }

        //BlockPos and Vector3 can be substituted for one another
        public static implicit operator Old_Vector3Int(Vector3 v)
        {
            Old_Vector3Int Old_Vector3Int = new Old_Vector3Int(
                Mathf.RoundToInt(v.x),
                Mathf.RoundToInt(v.y),
                Mathf.RoundToInt(v.z)
                );

            return Old_Vector3Int;
        }

        public static implicit operator Vector3(Old_Vector3Int pos)
        {
            return new Vector3(pos.x, pos.y, pos.z);
        }

        public static implicit operator Old_Vector3Int(Direction d)
        {
            switch (d)
            {
                case Direction.up:
                    return up;
                case Direction.down:
                    return down;
                case Direction.north:
                    return north;
                case Direction.south:
                    return south;
                case Direction.east:
                    return east;
                default:// Direction.west:
                    return west;
            }
        }

        public float Distance2(ref Old_Vector3Int pos)
        {
            int xx = x - pos.x;
            int yy = y - pos.y;
            int zz = z - pos.z;
            return xx * xx + yy * yy + zz * zz;
        }

        public static float Distance2(ref Old_Vector3Int pos1, ref Old_Vector3Int pos2)
        {
            int xx = pos1.x - pos2.x;
            int yy = pos1.y - pos2.y;
            int zz = pos1.z - pos2.z;
            return xx * xx + yy * yy + zz * zz;
        }

        //These operators let you add and subtract BlockPos from each other
        //or check equality with == and !=
        public static Old_Vector3Int operator -(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return pos1.Subtract(pos2);
        }

        public static Old_Vector3Int operator -(Old_Vector3Int pos)
        {
            return pos.Negate();
        }

        public static bool operator >(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return (pos1.x > pos2.x || pos1.y > pos2.y || pos1.z > pos2.z);
        }

        public static bool operator <(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return (pos1.x < pos2.x || pos1.y < pos2.y || pos1.z < pos2.z);
        }

        public static bool operator >=(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return (pos1.x >= pos2.x || pos1.y >= pos2.y || pos1.z >= pos2.z);
        }

        public static bool operator <=(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return (pos1.x <= pos2.x || pos1.y <= pos2.y || pos1.z <= pos2.z);
        }

        public static Old_Vector3Int operator +(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return pos1.Add(pos2);
        }

        public static Old_Vector3Int operator *(Old_Vector3Int pos, int i)
        {
            return new Old_Vector3Int(pos.x * i, pos.y * i, pos.z * i);
        }

        public static Old_Vector3Int operator *(Old_Vector3Int pos1, Old_Vector3Int pos2)
        {
            return new Old_Vector3Int(pos1.x * pos2.x, pos1.y * pos2.y, pos1.z * pos2.z);
        }

        #region Struct comparison

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = x;
                hashCode = (hashCode * 397) ^ y;
                hashCode = (hashCode * 397) ^ z;
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is Old_Vector3Int && Equals((Old_Vector3Int)obj);
        }

        public bool Equals(Old_Vector3Int other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public static bool operator ==(Old_Vector3Int a, Old_Vector3Int b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(Old_Vector3Int a, Old_Vector3Int b)
        {
            return a.x != b.x || a.y != b.y || a.z != b.z;
        }

        #endregion

        public override string ToString()
        {
            return "[" + x + ", " + y + ", " + z + "]";
        }
    }
}
