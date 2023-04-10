using UserControl = System.Windows.Controls.UserControl;

namespace Wox.Previewer.WindowsPreview
{
    /// <summary>
    /// Interaction logic for ExplorerPreview.xaml
    /// </summary>
    public partial class ExplorerPreview : UserControl
    {
        public ExplorerPreview()
        {
            InitializeComponent();
            if (DataContext is ExplorerPreviewViewModel vm)
            {
                vm.MainGrid = mainGrid;
                mainGrid.SizeChanged += (sender, e) =>
                {
                    vm.OnSizeChanged(e.NewSize.Width, e.NewSize.Height);
                };
            }
        }

        private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ExplorerPreviewViewModel vm)
            {
                vm.Unload();
            }
        }
    }
}
