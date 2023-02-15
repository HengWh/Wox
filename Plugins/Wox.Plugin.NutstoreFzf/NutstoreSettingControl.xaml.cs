using Api;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    /// <summary>
    /// Interaction logic for NutstoreSettingControl.xaml
    /// </summary>
    public partial class NutstoreSettingControl : UserControl
    {
        private FzfSetting _setting;
        public NutstoreSettingControl(FzfSetting fzfSetting)
        {
            InitializeComponent();
            _setting = fzfSetting;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //GRPC client
            var channel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            var api = new ApiService.ApiServiceClient(channel);
            var response = api.Stat(new StatRequest());
            var txt = JsonConvert.SerializeObject(response);
            Debug.WriteLine(txt);
            txtDbInfo.Text = txt;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_setting == null) return;

            txtCount.Text = _setting.MaxSearchCount.ToString();
            txtScore.Text=_setting.BaseScore.ToString();
            datagridUSN.ItemsSource = _setting.GetUsnStates();

        }
    }
}
