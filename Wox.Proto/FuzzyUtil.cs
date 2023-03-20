using Google.Protobuf;
using System.Text;

namespace Wox.Proto
{
    public static class FuzzyUtil
    {
        /// <summary>
        /// Unpackaged response data
        /// </summary>
        /// <param name="data">Fuzzy Search response value, the first byte means isDir, other bytes means full path.</param>
        /// <returns>isDir and full path</returns>
        public static (string path, bool isDir) UnpackValue(ByteString data)
        {
            var bytes = data.ToByteArray();
            var isDir = bytes[0] == 1;
            var path = Encoding.UTF8.GetString(bytes.Skip(1).ToArray());
            return (path, isDir);
        }

        /// <summary>
        /// Package full path and isDir to request Fuzzy Search
        /// </summary>
        /// <returns>The first byte means isDir, other bytes means full path.</returns>
        public static ByteString PackValue(string path, bool isDir)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var bytes = new byte[pathBytes.Length + 1];
            bytes[0] = (byte)(isDir ? 1 : 0);
            pathBytes.CopyTo(bytes, 1);
            return ByteString.CopyFrom(bytes);
        }

        /// <summary>
        /// Convert volume to db index.
        /// </summary>
        /// <param name="volume">Volume like "C" "D" "E"...... </param>
        /// <returns>Db index of "C" is 1, "D" is 2, "E" is 3, and so on. </returns>
        public static uint VolumeToDbIndex(string volume)
        {
            //Volume is C...Z,
            //ASCⅡ is 67...90.
            //Db index of C is 1

            int code = (int)volume.ToUpper()[0];
            return (uint)Math.Max(1, code - 66);
        }


        public static string DbIndexToVolume(uint index)
        {
            char c = (char)(index + 66);
            return c.ToString() + ":";
        }

    }
}
