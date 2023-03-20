using Api;
using Grpc.Core;
using Newtonsoft.Json;
using NLog;
using System.Windows;
using System.Windows.Controls;
using Wox.Proto;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    /// <summary>
    /// Interaction logic for NutstoreSettingControl.xaml
    /// </summary>
    public partial class NutstoreSettingControl : UserControl
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private FzfSetting _setting;
        private Action<FzfSetting> _saveAction;
        private ApiService.ApiServiceClient _api;

        public NutstoreSettingControl(FzfSetting fzfSetting, Action<FzfSetting> saveAction)
        {
            InitializeComponent();
            _setting = fzfSetting;
            _saveAction = saveAction;
            var channel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(channel);
        }

        private void btnQuery_Click(object sender, RoutedEventArgs e)
        {
            //GRPC client
            var response = _api.Stat(new StatRequest());
            if (response == null)
                return;

            var txt = JsonConvert.SerializeObject(response.EnvInfo);
            tbDbInfo.Text = txt;

            dataGridDbInfo.ItemsSource = response.Stats.Take(10);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_setting == null) return;

            txtCount.Text = _setting.MaxSearchCount.ToString();
            txtScore.Text = _setting.BaseScore.ToString();
            datagridUSN.ItemsSource = _setting.GetUsnStates();

        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            _setting.MaxSearchCount = Convert.ToInt32(txtCount.Text);
            _setting.BaseScore = Convert.ToInt32(txtScore.Text);
            _saveAction?.Invoke(_setting);
        }

        private void btnPurge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var purgeRequest = new PurgeRequest();

                purgeRequest.DbIdx.AddRange(_setting.UsnStates.Select(p => FuzzyUtil.VolumeToDbIndex(p.Volume)));
                _api.Purge(purgeRequest);

                _setting.UsnStates.Clear();
                datagridUSN.ItemsSource = null;
                _saveAction?.Invoke(_setting);

                btnQuery_Click(this, null);

            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Purge failed.");
            }
        }
    }
}
