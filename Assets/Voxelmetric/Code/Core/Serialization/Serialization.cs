using System.IO;
using UnityEngine;
using Voxelmetric.Code.Common.IO;

namespace Voxelmetric.Code.Core.Serialization
{
    public static class Serialization
    {
        private static string SaveLocation(string worldName)
        {
            string saveLocation = Directories.SAVE_FOLDER + "/" + worldName + "/";

            if (!Directory.Exists(saveLocation))
                Directory.CreateDirectory(saveLocation);

            return saveLocation;
        }

        private static string FileName(Vector3Int chunkLocation)
        {
            string fileName = chunkLocation.x + "," + chunkLocation.y + "," + chunkLocation.z + ".bin";
            return fileName;
        }

        private static string SaveFileName(Chunk chunk)
        {
            string saveFile = SaveLocation(chunk.World.WorldName);
            saveFile += FileName(chunk.Pos);
            return saveFile;
        }

        public static bool Write(Save save)
        {
            string path = SaveFileName(save.Chunk);
            return save.IsBinarizeNecessary() && FileHelpers.BinarizeToFile(path, save);
        }

        public static bool Read(Save save)
        {
            string path = SaveFileName(save.Chunk);
            return FileHelpers.DebinarizeFromFile(path, save);
        }
    }
}
