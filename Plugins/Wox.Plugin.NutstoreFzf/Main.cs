using Api;
using Grpc.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NLog;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.UsnParser;
using Wox.UsnParser.Native;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        const string NUTSTORE_FZF_EXE = "nutstore-fzf.exe";
        const string NUTSTORE_FZF_FEED_EXE = "nutstore-fzf-feed.exe";
        const string NUTSTORE_FZF_SERVER_EXE = "nutstore-fzf-server.exe";
        const string NUTSTORE_FZF_DIR = "FuzzyFinderSDK";

        private PluginInitContext _context;
        private FzfSetting _settings;
        private PluginJsonStorage<FzfSetting> _storage;
        private ApiService.ApiServiceClient _api;
        private string _pluginDir;
        private CancellationToken _token;

        public Control CreateSettingPanel()
        {
            return new NutstoreSettingControl(_settings);
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
            _pluginDir = _context.CurrentPluginMetadata.PluginDirectory;

            var cts = new CancellationTokenSource();
            _token = cts.Token;

            //start fzf.exe, fzf-feed.exe, fzf-server.exe
            var fzf = Path.Combine(_pluginDir, NUTSTORE_FZF_DIR, NUTSTORE_FZF_EXE);
            var fzf_feed = Path.Combine(_pluginDir, NUTSTORE_FZF_DIR, NUTSTORE_FZF_FEED_EXE);
            var fzf_server = Path.Combine(_pluginDir, NUTSTORE_FZF_DIR, NUTSTORE_FZF_SERVER_EXE);

            EnsureProcessStarted(fzf_server);
            EnsureProcessStarted(fzf);

            //GRPC client
            var channel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(channel);
            ////TODO: grpc heath check, heart?  


            //test
            var updateRequest = new UpdateRequest();
            var args = new UpdateRequest.Types.UpdateArgs()
            {
                TheType = Api.Type.ResidentSet,
                Key = 1,
                Val = Google.Protobuf.ByteString.CopyFromUtf8("C:\\123456.txt"),
                Deleted = false
            };
            updateRequest.Args.Add(args);
            var args2 = new UpdateRequest.Types.UpdateArgs()
            {
                TheType = Api.Type.ResidentSet,
                Key = 2,
                Val = Google.Protobuf.ByteString.CopyFromUtf8("C:\\123.txt"),
                Deleted = false
            };
            updateRequest.Args.Add(args2);
            var response = _api.Update(updateRequest);
            Debug.WriteLine(response);

            Task.Run(() =>
            {
                try
                {
                    //Push MFT and monitor USN
                    foreach (var device in DriveInfo.GetDrives())
                    {
                        var volume = device.Name.Substring(0, 1);
                        if (!volume.Contains("C"))
                            continue;

                        //Check UsnJournalId
                        using var journal = new UsnJournal(device);
                        var usnJournalData = journal.GetUsnJournalState();
                        if (!_settings.TryGetUsnState(device.Name, out DeviceUsnState deviceUsn))
                        {
                            deviceUsn = new DeviceUsnState() { Volume = device.Name };
                        }

                        if (deviceUsn.UsnJournalId != usnJournalData.UsnJournalID)
                        {
                            var entries = UsnHelper.SearchMasterFileTable(journal);
                            PushUsnEntries(entries);
                            deviceUsn.UsnJournalId = usnJournalData.UsnJournalID;
                        }
                        else
                        {
                            var entries = UsnHelper.ReadHistoryUsnJournals(journal, deviceUsn.USN);
                        }
                        deviceUsn.USN = (ulong)Math.Max(usnJournalData.NextUsn, 0);

                        _settings.AddOrUpdateUsnState(deviceUsn);
                        Save();

                        UsnHelper.MonitorRealTimeUsnJournal(PushUsnEntry, journal, usnJournalData, _token);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WoxWarn(ex.Message);
                }
            });
        }

        private void PushUsnEntries(IEnumerable<UsnEntry> usnEntries)
        {
            var updateRequest = new UpdateRequest();
            foreach (var entry in usnEntries)
            {
                var args = new UpdateRequest.Types.UpdateArgs()
                {
                    TheType = Api.Type.ResidentSet,
                    Key = entry.SecurityId,
                    Val = Google.Protobuf.ByteString.CopyFromUtf8(entry.Name),
                    Deleted = false
                };
                updateRequest.Args.Add(args);
            }
            _api.UpdateAsync(updateRequest);
        }

        private void PushUsnEntry(UsnEntry entry)
        {
            PushUsnEntries(new UsnEntry[] { entry });
            //Todo record Usn
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            try
            {
                //Search
                SearchRequest request = new SearchRequest();
                request.WithPos = true;
                request.Flags = 3; //1-OnlyFiles  2-OnlyDirs  3-All
                request.PrefixMask = "";
                var terms = query.Terms.Select(p => new SearchRequest.Types.QueryTerm() { Term = p, CaseSensitive = false });
                request.Terms.AddRange(terms);

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                using var response = _api.Search(request, cancellationToken: cts.Token);

                while (response.ResponseStream.MoveNext(cts.Token).Result)
                {
                    var serchResponse = response.ResponseStream.Current;
                    foreach (var item in serchResponse.Results)
                    {
                        Result result = new Result();
                        result.Score = _settings.BaseScore + item.Score;
                        result.Title = item.Val.ToString();
                        result.SubTitle = $"[Nutstore FZF] {result.Title}";
                        result.IcoPath = "Images/nutstore.png";
                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                //Test
                var path = @"D:\Code\Wox\nutstore-fzf-windows\nutstore-fzf.exe";
                results.Add(new Result()
                {
                    Title = "Programmer is coding...",
                    SubTitle = $"[Nutstore FZF] {path}",
                    IcoPath = "Images/nutstore.png",
                    Score = _settings.BaseScore,
                });
            }

            return results;
        }

        public void Save()
        {
            _storage?.Save();
        }

        private bool EnsureProcessStarted(string fileName)
        {
            var oldProcess = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fileName));
            if (oldProcess != null && oldProcess.Length > 0)
            {
                return true;
            }
            try
            {
                var process = new Process();
                process.StartInfo.FileName = fileName;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                return process.Start();
            }
            catch (Exception ex)
            {
                Logger.WoxWarn($"Failed to start process {fileName}.\n{ex}");
                return false;
            }
        }
    }
}