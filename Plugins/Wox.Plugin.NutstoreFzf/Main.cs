using Api;
using Grpc.Core;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable
    {
        const string NUTSTORE_FZF_EXE = "nutstore-fzf.exe";
        const string NUTSTORE_FZF_FEED_EXE = "nutstore-fzf-feed.exe";
        const string NUTSTORE_FZF_SERVER_EXE = "nutstore-fzf-server.exe";
        const string NUTSTORE_FZF_DIR = "FuzzyFinderSDK";


        private PluginInitContext _context;
        private FzfSetting _settings;
        private PluginJsonStorage<FzfSetting> _storage;
        private ApiService.ApiServiceClient _api;

        public Control CreateSettingPanel()
        {
            return new NutstoreSettingControl();
        }

        public string GetTranslatedPluginDescription()
        {
            return _context.API.GetTranslation("wox_plugin_NutstoreFzf_plugin_description");
        }

        public string GetTranslatedPluginTitle()
        {
            return _context.API.GetTranslation("wox_plugin_NutstoreFzf_plugin_name");
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _storage = new PluginJsonStorage<FzfSetting>();
            _settings = _storage.Load();

            //start fzf.exe, fzf-feed.exe, fzf-server.exe
            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            var fzf = Path.Combine(pluginDirectory, NUTSTORE_FZF_DIR, NUTSTORE_FZF_EXE);
            var fzf_feed = Path.Combine(pluginDirectory, NUTSTORE_FZF_DIR, NUTSTORE_FZF_FEED_EXE);
            var fzf_server = Path.Combine(pluginDirectory, NUTSTORE_FZF_DIR, NUTSTORE_FZF_SERVER_EXE);
            EnsureProcessStarted(fzf);
            EnsureProcessStarted(fzf_feed);
            EnsureProcessStarted(fzf_server);

            //GRPC client
            var channel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(channel);
            //TODO: grpc heath check, heart?  



            //TODO: push MFT and monitor USN
            foreach (var device in DriveInfo.GetDrives())
            {
                //Check UsnJournalId
                //using var Journal = new UsnJournal(driveInfo);
                //var usnState = Journal.GetUsnJournalState();

                //Create UsnJournal??

                //if(_settings.UsnJournalId==0 || _settings.UsnJournalId != usnState.journalId)
                //  Read MFT and push to fzf-server
                //
                //_settings.UsnJournalId=usnState.journalId;
                //Monitor USN and push to fzf-server

            }
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();

            //Search
            SearchRequest request = new SearchRequest();
            request.WithPos = true;
            request.Flags = 3; //1-OnlyFiles  2-OnlyDirs  3-All
            var terms = query.Terms.Select(p => new SearchRequest.Types.QueryTerm() { Term = p, CaseSensitive = false });
            request.Terms.AddRange(terms);
            var responseStream = _api.Search(request).ResponseStream;

            while (!responseStream.MoveNext().Result)
            {
                var response = responseStream.Current;
                foreach (var item in response.Results)
                {
                    Result result = new Result();
                    //TODO:
                    result.Score = _settings.BaseScore + item.Score;
                }
            }


            //TODO serch from fzf.exe
            var path = @"D:\Code\Wox\nutstore-fzf-windows\nutstore-fzf.exe";
            results.Add(new Result()
            {
                Title = "Programmer is coding...",
                SubTitle = $"[Nutstore FZF] {path}",
                IcoPath = "Images/nutstore.png",
                Score = _settings.BaseScore,
            });


            return results;
        }

        public void Save()
        {
            _storage?.Save();
        }

        private bool EnsureProcessStarted(string fileName)
        {
            var oldProcess = Process.GetProcessesByName(Path.GetFileName(fileName));
            if (oldProcess != null && oldProcess.Length > 0)
            {
                return true;
            }
            try
            {
                var process = new Process();
                process.StartInfo.FileName = fileName;
                return process.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start process {fileName}.\n{ex}");
                return false;
            }
        }
    }
}