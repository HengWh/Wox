using System.Windows;
using System.Windows.Input;
using Wox.Plugin;

namespace Wox.ViewModel
{
    public class PreviewViewModel : BaseModel
    {
        public ICommand EscCommand { get; set; }
        public FrameworkElement PreviewContent { get; set; }
        public Visibility MainWindowVisibility { get; set; }

        public PreviewViewModel()
        {
            EscCommand = new RelayCommand(_ =>
            {
                MainWindowVisibility = Visibility.Collapsed;
            });
        }
    }
}
