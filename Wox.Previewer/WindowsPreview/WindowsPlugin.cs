using System.IO;

namespace Wox.Previewer.WindowsPreview
{
    public class WindowsPlugin
    {
        private ExplorerPreview _explorerPreview;

        public bool CanView(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            if (ExplorerPreviewViewModel.GetPreviewHandlerGUID(filePath) != System.Guid.Empty)
            {
                return true;
            }
            return false;
        }

        public ExplorerPreview? Preview(string filePath)
        {
            _explorerPreview ??= new ExplorerPreview();
            if (_explorerPreview.DataContext is ExplorerPreviewViewModel vm)
            {
                if (vm.LoadFile(filePath))
                    return _explorerPreview;
            }
            return null;


        }
    }
}
