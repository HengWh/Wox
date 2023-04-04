using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Wox.Previewer;
using Wox.ViewModel;

namespace Wox.Helper
{
    public class PreviewService
    {
        private PreviewWindow _previewWindow;
        private PreviewViewModel _viewModel;
        private CancellationTokenSource _cts;
        private static Lazy<PreviewService> _instance = new Lazy<PreviewService>(() => new PreviewService());

        public static PreviewService Instance => _instance.Value;
        public bool IsPreviewing { get; set; }

        private PreviewService()
        {
            PreviewServer.Initializer();
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

        public void PreviewWindow(ResultViewModel model)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _cts = new CancellationTokenSource();

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
                ShowImageWindow(model.Image.Value, file, _cts.Token);
        }

        public FrameworkElement GetThumbnailImagel(ResultViewModel model)
        {
            var imageControl = new System.Windows.Controls.Image();
            imageControl.Source = model.Image.Value;
            return imageControl;
        }

        public async Task<FrameworkElement?> GetPreviewer(ResultViewModel model)
        {
            if (model == null || model.Result == null)
                return null;

            if (_cts != null && _cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            await Task.Delay(500);
            if (token.IsCancellationRequested)
                return null;
            var filePath = model.Result.SubTitle;
            if (!File.Exists(filePath))
                return null;
            var element = PreviewServer.Preview(filePath);

            if (token.IsCancellationRequested)
                return null;
            else
                return element;
        }

        public void Preview(ResultViewModel model)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _cts = new CancellationTokenSource();

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
                ShowImageWindow(model.Image.Value, file, _cts.Token);
        }


        private async void ShowImageWindow(ImageSource thumbnail, string filePath = null, CancellationToken token = default(CancellationToken))
        {
            var imageControl = new System.Windows.Controls.Image();
            imageControl.Source = thumbnail;
            _viewModel.PreviewContent = imageControl;

            if (!File.Exists(filePath))
                return;

            await Task.Delay(200);

            if (token.IsCancellationRequested)
                return;

            _viewModel.PreviewContent = PreviewServer.Preview(filePath);

        }

    }
}
