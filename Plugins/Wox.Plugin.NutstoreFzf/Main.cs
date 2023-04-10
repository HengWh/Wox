using Api;
using Grpc.Core;
using NLog;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using Wox.Proto;
using static Api.SearchResponse.Types;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, ISavable, IContextMenu, IResultUpdated
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private PluginInitContext _context;
        private FzfSetting _settings;
        private PluginJsonStorage<FzfSetting> _storage;
        private ApiService.ApiServiceClient _api;
        private Usn.UsnClient _usn;
        private CancellationTokenSource _cts;
        private Comparison<SearchResult> _comparsion;
        private Comparison<SearchResult> _comparsionKey;
        private DateTime _queryFinishedTime = DateTime.UtcNow;

        public event ResultUpdatedEventHandler ResultsUpdated;

        public Action<List<Result>> UpdateAction { get; set; }

        public Control CreateSettingPanel()
        {
            return new NutstoreSettingControl(_settings, item =>
            {
                _settings.MaxSearchCount = item.MaxSearchCount;
                _settings.BaseScore = item.BaseScore;
                _settings.UsnStates = item.UsnStates;
                _storage?.Save();
            });
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

            //GRPC client
            var apiChannel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(apiChannel);

            var usnChannel = new Channel("127.0.0.1:38998", ChannelCredentials.Insecure);
            _usn = new Usn.UsnClient(usnChannel);
            //TODO: grpc heath check, heart?  

            _comparsionKey = new Comparison<SearchResult>((a, b) => a.Key == b.Key ? 0 : 1);

            _comparsion = new Comparison<SearchResult>((a, b) =>
            {
                if (a.DbType != b.DbType && (a.DbType < DbType.Fs || b.DbType < DbType.Fs))
                {
                    return b.DbType - a.DbType;
                }

                if (a.Score != b.Score)
                    return a.Score - b.Score;

                var pathA = FuzzyUtil.UnpackValue(a.Val);
                var pathB = FuzzyUtil.UnpackValue(b.Val);
                if (pathA.Length == pathB.Length)
                {
                    if (pathA.Equals(pathB, StringComparison.OrdinalIgnoreCase))
                        return 0;
                    return -1;
                }
                else
                {
                    return pathB.Length - pathA.Length;
                }

            });

            //Push MFT and monitor USN
            foreach (var drive in DriveInfo.GetDrives())
            {
                Task.Run(() => MonitorDiviceUsnJournal(drive));
            }
        }

        public List<Result> Query(Query query)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                Logger.Debug($"Cancel initialize {_cts.Token.GetHashCode()} {Thread.CurrentThread.ManagedThreadId} {query.RawQuery}");
                _cts.Dispose();
            }

            var source = new CancellationTokenSource();
            _cts = source;
            var token = source.Token;

            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.Search))
            {
                return results;
            }

            try
            {
                //Search
                var startTime = DateTime.UtcNow;
                var minHeap = new MinHeap<SearchResult>(_comparsion);
                SearchRequest request = new SearchRequest();
                request.WithPos = true;
                request.Flags = 3; //1-OnlyFiles  2-OnlyDirs  3-All
                //request.PrefixMask = "";
                var terms = query.Terms.Select(p => new SearchRequest.Types.QueryTerm() { Term = p, CaseSensitive = false });
                request.Terms.AddRange(terms);

                using var response = _api.Search(request, cancellationToken: token);
                while (response.ResponseStream.MoveNext().Result)
                {
                    if (token.IsCancellationRequested)
                        return results;
                    var serchResponse = response.ResponseStream.Current;
                    foreach (var item in serchResponse.Results)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        if (minHeap.Contains(item, _comparsionKey))
                            continue;

                        if (minHeap.Count < _settings.MaxSearchCount)
                        {
                            minHeap.Push(item);
                        }
                        else
                        {
                            if (DateTime.UtcNow - _queryFinishedTime > TimeSpan.FromMilliseconds(500))
                            {
                                var tmpResult = RankTopResult(minHeap.Clone(), token);
                                _queryFinishedTime = DateTime.UtcNow;
                                ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs() { Query = query, Results = tmpResult });
                            }
                            if (_comparsion(minHeap.Peek(), item) < 0)
                            {
                                minHeap.Pop();
                                minHeap.Push(item);
                            }
                        }
                    }

                    if (token.IsCancellationRequested)
                        return results;
                }

                results = RankTopResult(minHeap, token);
                _queryFinishedTime = DateTime.UtcNow;
                _cts = null;

                var endTime=DateTime.UtcNow;
                Logger.Info($"Query end.. Terms is {query.Search}. Time span is {(endTime-startTime).TotalMilliseconds}ms");

                return results;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex);
            }

            return results;
        }

        private List<Result> RankTopResult(MinHeap<SearchResult> minHeap, CancellationToken token)
        {
            List<Result> results = new List<Result>();
            while (minHeap.Count > 0)
            {
                if (token.IsCancellationRequested)
                    return results;
                var item = minHeap.Pop();

                var path = FuzzyUtil.UnpackValue(item.Val);
                Result result = new Result();
                result.Score = _settings.BaseScore;
                result.Title = Path.GetFileName(path);
                result.SubTitle = path;
                result.IcoPath = path;
                var titleIndex = path.LastIndexOf('\\') + 1;
                result.TitleHighlightData = item.Pos.Select(p => (int)p - titleIndex).ToList();
                result.SubTitleHighlightData = item.Pos.Select(p => (int)p).ToList();
                result.ContextData = item.Key;
                result.Action = c =>
                {
                    bool hide;
                    try
                    {
                        Feedback(result, item.DbIdx, item.Key);
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(path),
                        });
                        hide = true;
                    }
                    catch (Win32Exception)
                    {
                        var name = $"Plugin: {_context.CurrentPluginMetadata.Name}";
                        var message = "Can't open this file";
                        _context.API.ShowMsg(name, message, string.Empty);
                        hide = false;
                    }
                    return hide;
                };
                results.Add(result);
            }
            results.Reverse();
            return results;
        }

        private void MonitorDiviceUsnJournal(DriveInfo drive)
        {
            try
            {
                var volume = drive.Name.Substring(0, 1);

                //Check UsnJournalId
                var journal = new Journal() { Volume = volume };
                var journalData = _usn.GetJournalData(journal);
                if (!_settings.TryGetUsnState(volume, out DeviceUsnState deviceUsn))
                {
                    deviceUsn = new DeviceUsnState()
                    {
                        Volume = volume,
                        USN = journalData.NextUsn
                    };
                }
                if (deviceUsn.UsnJournalId != journalData.JournalId)
                {
                    _usn.PushMasterFileTable(journal);
                    deviceUsn.UsnJournalId = journalData.JournalId;
                }

                _usn.PushUsnHistoryAsync(new JournalData()
                {
                    Volume = deviceUsn.Volume,
                    JournalId = deviceUsn.UsnJournalId,
                    NextUsn = deviceUsn.USN
                });

                _usn.MonitorUsnAsync(journal);

                deviceUsn.USN = journalData.NextUsn;

                _settings.AddOrUpdateUsnState(deviceUsn);
                _storage?.Save();

            }
            catch (Exception ex)
            {
                Logger.WoxWarn(ex.Message);
            }
        }

        private void Feedback(Result result, uint dbIndex, ulong key)
        {
            try
            {
                var request = new UpdateRequest();
                request.Args.Add(new UpdateRequest.Types.UpdateArgs()
                {
                    DbIdx = dbIndex,
                    DbType = DbType.History,
                    Key = key,
                    Val = FuzzyUtil.PackValue(result.SubTitle, Directory.Exists(result.SubTitle))
                });
                _api.Update(request);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex);
            }
        }

        public void Save()
        {
            foreach (var item in _settings?.UsnStates)
            {
                var journal = new Journal() { Volume = item.Volume };
                var journalData = _usn.GetJournalData(journal);
                item.USN = journalData.NextUsn;
            }
            _storage?.Save();
        }

        #region ContextMenu

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var dbIndex = FuzzyUtil.VolumeToDbIndex(Path.GetPathRoot(selectedResult.SubTitle));
            Feedback(selectedResult, dbIndex, (ulong)selectedResult.ContextData);
            List<Result> contextMenus = new List<Result>();
            if (selectedResult == null) return contextMenus;

            List<FuzzyContextMenu> availableContextMenus = new List<FuzzyContextMenu>();
            availableContextMenus.AddRange(GetDefaultContextMenu());

            var isFolder = Directory.Exists(selectedResult.SubTitle);

            if (!isFolder)
            {
                foreach (FuzzyContextMenu contextMenu in availableContextMenus)
                {
                    var menu = contextMenu;
                    contextMenus.Add(new Result
                    {
                        Title = contextMenu.Name,
                        Action = _ =>
                        {
                            string argument = menu.Argument.Replace("{path}", selectedResult.SubTitle);
                            try
                            {
                                Process.Start(menu.Command, argument);
                            }
                            catch
                            {
                                _context.API.ShowMsg(string.Format(_context.API.GetTranslation("wox_plugin_NutstoreFzf_canot_start"), selectedResult.SubTitle), string.Empty, string.Empty);
                                return false;
                            }
                            return true;
                        },
                        IcoPath = contextMenu.ImagePath
                    });
                }
            }

            var icoPath = isFolder ? "Images\\folder.png" : "Images\\file.png";
            contextMenus.Add(new Result
            {
                Title = _context.API.GetTranslation("wox_plugin_everything_copy_path"),
                Action = (context) =>
                {
                    Clipboard.SetText(selectedResult.SubTitle);
                    return true;
                },
                IcoPath = icoPath
            });

            contextMenus.Add(new Result
            {
                Title = _context.API.GetTranslation("wox_plugin_everything_copy"),
                Action = (context) =>
                {
                    Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { selectedResult.SubTitle });
                    return true;
                },
                IcoPath = icoPath
            });

            contextMenus.Add(new Result
            {
                Title = _context.API.GetTranslation("wox_plugin_NutstoreFzf_delete"),
                Action = (context) =>
                {
                    try
                    {
                        if (!isFolder)
                            File.Delete(selectedResult.SubTitle);
                        else
                            Directory.Delete(selectedResult.SubTitle);
                    }
                    catch
                    {
                        _context.API.ShowMsg(string.Format(_context.API.GetTranslation("wox_plugin_NutstoreFzf_canot_delete"), selectedResult.SubTitle), string.Empty, string.Empty);
                        return false;
                    }

                    return true;
                },
                IcoPath = icoPath
            });

            return contextMenus;
        }

        private List<FuzzyContextMenu> GetDefaultContextMenu()
        {
            List<FuzzyContextMenu> defaultContextMenus = new List<FuzzyContextMenu>();
            FuzzyContextMenu openFolderContextMenu = new FuzzyContextMenu
            {
                Name = _context.API.GetTranslation("wox_plugin_NutstoreFzf_open_containing_folder"),
                Command = "explorer.exe",
                Argument = " /select,\"{path}\"",
                ImagePath = "Images\\folder.png"
            };

            defaultContextMenus.Add(openFolderContextMenu);
            return defaultContextMenus;
        }

        #endregion
    }
}