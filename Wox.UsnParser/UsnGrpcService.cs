using Api;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using NLog;
using System.Diagnostics;
using Wox.Proto;
using Wox.UsnParser.Native;

namespace Wox.UsnParser
{
    public class UsnGrpcService : Usn.UsnBase
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ApiService.ApiServiceClient _api;
        private readonly CancellationTokenSource _cancelTokenSource;
        private readonly CancellationToken _token;

        public UsnGrpcService()
        {
            var apiChannel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(apiChannel);

            _cancelTokenSource = new CancellationTokenSource();
            _token = _cancelTokenSource.Token;
        }

        public override Task<JournalData> GetJournalData(Journal request, ServerCallContext context)
        {

            var usnJournal = new UsnJournal(request.Volume);
            var state = usnJournal.GetUsnJournalState();
            logger.Info($"GetJournalData succeeded. Volume is {request.Volume}, JournaleId is {state.UsnJournalID}, NextUsn is {state.NextUsn}.");
            return Task.FromResult(new JournalData()
            {
                Volume = request.Volume,
                JournalId = state.UsnJournalID,
                NextUsn = (ulong)state.NextUsn
            });
        }

        public override Task<Empty> PushMasterFileTable(Journal request, ServerCallContext context)
        {
            try
            {
                logger.Info($"PushMasterFileTable start, volume is {request.Volume}.");
                var stopwatch = Stopwatch.StartNew();
                var usnJournal = new UsnJournal(request.Volume);
                var entries = UsnHelper.SearchMasterFileTable(usnJournal);

                var count = PushUsnEntries(usnJournal, entries);
                stopwatch.Stop();
                logger.Info($"PushMasterFileTable succeeded.{request.Volume} {count} entries time span {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PushMasterFileTable failed.");
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> PushUsnHistory(JournalData request, ServerCallContext context)
        {
            try
            {
                var usnJournal = new UsnJournal(request.Volume);
                var entries = UsnHelper.ReadHistoryUsnJournals(usnJournal, request.NextUsn);
                foreach (var entry in entries)
                {
                    FilterAndPushUsnEntry(usnJournal, entry);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"PushUsnHistory failed.");
            }

            return Task.FromResult(new Empty());
        }

        public override Task<Empty> MonitorUsn(Journal request, ServerCallContext context)
        {
            try
            {
                logger.Info($"MoonitorUsn {request.Volume} is started.");
                var usnJournal = new UsnJournal(request.Volume);
                UsnHelper.MonitorRealTimeUsnJournal(entry => FilterAndPushUsnEntry(usnJournal, entry), usnJournal, _token);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"MoonitorUsn {request.Volume} failed.");
            }
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CancelMoonitorUsn(Journal request, ServerCallContext context)
        {
            logger.Info($"CancelMoonitorUsn {request.Volume}");
            _cancelTokenSource.Cancel();
            return Task.FromResult(new Empty());
        }

        private ulong PushUsnEntries(UsnJournal journal, IEnumerable<UsnEntry> usnEntries)
        {
            ulong count = 0;
            var parentDictionary = new Dictionary<ulong, string>();
            var dirList = new List<UsnEntry>();
            var fileList = new List<UsnEntry>();

            var updateRequest = new UpdateRequest();
            string volume = journal.VolumeName.TrimEnd('\\');
            var db_index = FuzzyUtil.VolumeToDbIndex(volume);

            foreach (var entry in usnEntries)
            {
                if (entry.IsFolder)
                    dirList.Add(entry);
                else
                    fileList.Add(entry);
            }

            foreach (var dirEntry in dirList)
            {
                string parent = string.Empty;
                if (parentDictionary.TryGetValue(dirEntry.ParentFileReferenceNumber, out parent)
                    || journal.TryGetPathFromFileId(dirEntry.ParentFileReferenceNumber, out parent))
                {
                    count++;
                    if (!parentDictionary.ContainsKey(dirEntry.ParentFileReferenceNumber))
                        parentDictionary.Add(dirEntry.ParentFileReferenceNumber, parent);
                    parentDictionary.Add(dirEntry.FileReferenceNumber, Path.Combine(parent, dirEntry.Name));
                    updateRequest.Args.Add(new UpdateRequest.Types.UpdateArgs()
                    {
                        DbIdx = db_index,
                        DbType = DbType.Fs,
                        Key = dirEntry.FileReferenceNumber,
                        Val = FuzzyUtil.PackValue(Path.Combine(volume + parent, dirEntry.Name), true),
                        Deleted = false
                    });
                    logger.Info($"Push folder entry. Path is {Path.Combine(volume + parent, dirEntry.Name)}, deleted is false.");
                }
            }
            dirList.Clear();

            foreach (var fileEntry in fileList)
            {
                string parent = string.Empty;
                if (parentDictionary.TryGetValue(fileEntry.ParentFileReferenceNumber, out parent))
                {
                    count++;
                    updateRequest.Args.Add(new UpdateRequest.Types.UpdateArgs()
                    {
                        DbIdx = db_index,
                        DbType = DbType.Fs,
                        Key = fileEntry.FileReferenceNumber,
                        Val = FuzzyUtil.PackValue(Path.Combine(volume + parent, fileEntry.Name), false),
                        Deleted = false
                    });
                    logger.Info($"Push file entry. Key is {fileEntry.FileReferenceNumber}, path is {Path.Combine(volume + parent, fileEntry.Name)}, deleted is false.");
                }
            }
            parentDictionary.Clear();
            fileList.Clear();

            Task.Run(() =>
            {
                try
                {
                    _api.UpdateAsync(updateRequest);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Update request error");
                }
            });
            return count;
        }

        private void FilterAndPushUsnEntry(UsnJournal journal, UsnEntry entry)
        {
            const uint renameOldName = (uint)UsnReason.RENAME_OLD_NAME;
            const uint renameNewName = (uint)UsnReason.RENAME_NEW_NAME | (uint)UsnReason.CLOSE;
            const uint delete = (uint)UsnReason.FILE_DELETE | (uint)UsnReason.CLOSE;
            const uint create = (uint)UsnReason.FILE_CREATE | (uint)UsnReason.CLOSE;

            var isDelete = entry.Reason == renameOldName || entry.Reason == delete;
            var isCreate = entry.Reason == renameNewName || entry.Reason == create;
            if (!isCreate && !isDelete)
                return;

            string volume = journal.VolumeName.TrimEnd('\\');
            var updateRequest = new UpdateRequest();
            var args = new UpdateRequest.Types.UpdateArgs()
            {
                DbIdx = FuzzyUtil.VolumeToDbIndex(journal.VolumeName),
                DbType = DbType.Fs,
                Key = entry.FileReferenceNumber,
                Val = Google.Protobuf.ByteString.Empty,
                Deleted = isDelete
            };

            if (isCreate)
            {
                if (journal.TryGetPathFromFileId(entry.ParentFileReferenceNumber, out var path))
                {
                    var fullPath = Path.Combine(volume + path, entry.Name);
                    args.Val = FuzzyUtil.PackValue(fullPath, entry.IsFolder);
                }
                else
                {
                    logger.Warn($"Why get parent path is null?\nName={entry.Name}, FileKey={entry.FileReferenceNumber}, ParentKey={entry.ParentFileReferenceNumber}.");
                    return;
                }
            }
            logger.Info($"Push file entry. Key is {args.Key}, path is {entry.Name}, deleted is {isDelete}.");
            updateRequest.Args.Add(args);
            try
            {
                _api.UpdateAsync(updateRequest);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Update failed. file name is {entry.Name}");
            }
        }
    }
}
