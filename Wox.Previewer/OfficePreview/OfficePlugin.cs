using System.IO;

namespace Wox.Previewer.OfficePreview
{
    public class OfficePlugin
    {
        private static readonly string[] SupportedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv" };
        private ExplorerPreview _explorerPreview;

        public bool CanView(string filePath)
        {
            if (!File.Exists(filePath))
                return false;
            //var ext = Path.GetExtension(filePath);
            //if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
            //    return false;
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
                vm.LoadFile(filePath);
            }
            return _explorerPreview;


        }
    }
}
