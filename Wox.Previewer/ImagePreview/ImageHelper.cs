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

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Windows.Media.PixelFormat;

namespace QuickLook.Plugin.ImageViewer
{
    public static class ImageHelper
    {
        public static Uri FilePathToFileUrl(string filePath)
        {
            var uri = new StringBuilder();
            foreach (var v in filePath)
                if (v >= 'a' && v <= 'z' || v >= 'A' && v <= 'Z' || v >= '0' && v <= '9' ||
                    v == '+' || v == '/' || v == ':' || v == '.' || v == '-' || v == '_' || v == '~' ||
                    v > '\x80')
                    uri.Append(v);
                else if (v == Path.DirectorySeparatorChar || v == Path.AltDirectorySeparatorChar)
                    uri.Append('/');
                else
                    uri.Append($"%{(int)v:X2}");
            if (uri.Length >= 2 && uri[0] == '/' && uri[1] == '/') // UNC path
                uri.Insert(0, "file:");
            else
                uri.Insert(0, "file:///");

            try
            {
                return new Uri(uri.ToString());
            }
            catch
            {
                return null;
            }
        }

        public static BitmapSource ToBitmapSource(this Bitmap source)
        {
            var orgSource = source;
            BitmapSource bs = null;
            try
            {
                var data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
                    ImageLockMode.ReadOnly, source.PixelFormat);

                // BitmapSource.Create throws an exception when the image is scanned backward.
                // The Clone() will make it back scanning forward.
                if (data.Stride < 0)
                {
                    source.UnlockBits(data);
                    source = (Bitmap)source.Clone();
                    data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
                        ImageLockMode.ReadOnly, source.PixelFormat);
                }

                bs = BitmapSource.Create(source.Width, source.Height, Math.Floor(source.HorizontalResolution),
                    Math.Floor(source.VerticalResolution), ConvertPixelFormat(source.PixelFormat), null,
                    data.Scan0, data.Stride * source.Height, data.Stride);

                source.UnlockBits(data);

                bs.Freeze();
            }
            catch
            {
                // ignored
            }
            finally
            {
                if (orgSource != source)
                    source.Dispose();
            }

            return bs;
        }

        public static Bitmap ToBitmap(this BitmapSource source)
        {
            using (var outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(source));
                enc.Save(outStream);
                var bitmap = new Bitmap(outStream);

                return new Bitmap(bitmap);
            }
        }

        private static PixelFormat ConvertPixelFormat(
            System.Drawing.Imaging.PixelFormat sourceFormat)
        {
            switch (sourceFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return PixelFormats.Bgr24;

                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    return PixelFormats.Bgra32;

                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    return PixelFormats.Bgr32;
            }

            return new PixelFormat();
        }
    }
}