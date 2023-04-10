// Copyright © 2020 Paddy Xu
// 
// This file is part of QuickLook program.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using ImageMagick;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Size = System.Windows.Size;

namespace QuickLook.Plugin.ImageViewer
{
    public class MetaProvider
    {
        private readonly SortedDictionary<string, (string, string)> _cache =
            new SortedDictionary<string, (string, string)>(); // [key, [label, value]]

        private readonly string _path;

        public MetaProvider(string path)
        {
            _path = path;

            GetExif();
        }

        public SortedDictionary<string, (string, string)> GetExif()
        {
            if (_cache.Count != 0)
                return _cache;

            return _cache;
        }

        public byte[] GetThumbnail()
        {
            return NativeMethods.GetThumbnail(_path) ?? new byte[0];
        }

        public Size GetSize()
        {
            _cache.TryGetValue("_.Size.Width", out var w_);
            _cache.TryGetValue("_.Size.Height", out var h_);

            if (int.TryParse(w_.Item2, out var w) && int.TryParse(h_.Item2, out var h))
                return new Size(w, h);

            // fallback

            using (var mi = new MagickImage())
            {
                mi.Ping(_path);
                w = mi.Width;
                h = mi.Height;
            }

            return w + h == 0 ? new Size(800, 600) : new Size(w, h);
        }

        public Orientation GetOrientation()
        {
            return (Orientation)NativeMethods.GetOrientation(_path);
        }
    }

    internal static class NativeMethods
    {
        public static byte[] GetThumbnail(string file)
        {
            try
            {
                var len = GetThumbnail_64(file, null);
                if (len <= 0)
                    return null;

                var buffer = new byte[len];
                var _ = GetThumbnail_64(file, buffer);

                return buffer;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
        }

        public static int GetOrientation(string file)
        {
            try
            {
                return GetOrientation_64(file);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return 0;
            }
        }

        [DllImport("exiv2-ql-64.dll", EntryPoint = "GetThumbnail", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetThumbnail_64([MarshalAs(UnmanagedType.LPWStr)] string file,
            [MarshalAs(UnmanagedType.LPArray)] byte[] buffer);

        [DllImport("exiv2-ql-64.dll", EntryPoint = "GetOrientation", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetOrientation_64([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    public enum Orientation
    {
        Undefined = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomRight = 3,
        BottomLeft = 4,
        LeftTop = 5,
        RightTop = 6,
        RightBottom = 7,
        LeftBottom = 8
    }
}