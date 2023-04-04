using QuickLook.Plugin.ImageViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wox.Previewer.OfficePreview;

namespace Wox.Previewer
{
    public static class PreviewServer
    {
        private static ImagePlugin _imagePlugin;
        private static OfficePlugin _officePlugin;
        public static void Initializer()
        {
            _imagePlugin = new ImagePlugin();
            _imagePlugin.Init();

            _officePlugin = new OfficePlugin();

        }

        public static FrameworkElement? Preview(string path)
        {
            if (_imagePlugin.CanHandle(path))
                return _imagePlugin.Preview(path);
            else if (_officePlugin.CanView(path))
                return _officePlugin.Preview(path);
            return null;
        }
    }
}
