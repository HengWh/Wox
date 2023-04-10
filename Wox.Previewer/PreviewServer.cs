using QuickLook.Plugin.ImageViewer;
using System.Windows;
using Wox.Previewer.WindowsPreview;

namespace Wox.Previewer
{
    public static class PreviewServer
    {
        private static ImagePlugin _imagePlugin;
        private static WindowsPlugin _officePlugin;
        public static void Initializer()
        {
            _imagePlugin = new ImagePlugin();
            _imagePlugin.Init();

            _officePlugin = new WindowsPlugin();

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
