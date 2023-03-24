using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using Wox.ViewModel;

namespace Wox.Helper
{
    public class PreviewService
    {
        private PreviewWindow _previewWindow;
        private PreviewViewModel _viewModel;
        private static Lazy<PreviewService> _instance = new Lazy<PreviewService>(() => new PreviewService());

        public static PreviewService Instance => _instance.Value;
        public bool IsPreviewing { get; set; }

        private PreviewService()
        {
        }

        private void InitPreviewWindow()
        {
            if (_previewWindow == null || _viewModel == null)
            {
                _previewWindow = new PreviewWindow();
                _viewModel = new PreviewViewModel();
                _previewWindow.DataContext = _viewModel;
                _previewWindow.Closed += previewWindow_Closed;
                _previewWindow.Activated += (sender, e) => IsPreviewing = true;
                _previewWindow.Deactivated += (sender, e) => IsPreviewing = false;
            }
        }

        private void previewWindow_Closed(object sender, EventArgs e)
        {
            _previewWindow = null;
            _viewModel = null;
        }

        public void Preview(ResultViewModel model)
        {
            if (model == null || model.Result == null)
                return;

            InitPreviewWindow();

            IsPreviewing = true;
            var file = model.Result.SubTitle;

            if (!File.Exists(file))
                ShowImageWindow(model.Image.Value);
            else if (!Path.HasExtension(file))
                ShowImageWindow(model.Image.Value);
            else
                ShowImageWindow(model.Image.Value);


        }

        private void ShowImageWindow(ImageSource source)
        {
            if (App.Current.MainWindow is not MainWindow owner)
                return;

            var imageControl = new System.Windows.Controls.Image();
            imageControl.Source = source;
            _viewModel.PreviewContent = imageControl;

            owner.Dispatcher.BeginInvoke(() =>
            {
                _previewWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                _previewWindow.WindowState = System.Windows.WindowState.Normal;
                _previewWindow.Show();
            });
        }

    }
}
