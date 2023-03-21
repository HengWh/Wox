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
        private readonly Dictionary<string, string> PathDictionary = new Dictionary<string, string>();

        public UsnGrpcService()
        {
            var apiChannel = new Channel("127.0.0.1:38999", ChannelCredentials.Insecure);
            _api = new ApiService.ApiServiceClient(apiChannel);

            _cancelTokenSource = new CancellationTokenSource();
            _token = _cancelTokenSource.Token;
        }

        public override Task<JournalData> GetJournalData(Journal request, ServerCallContext context)
        {
            logger.Info($"GetJournalData request is {request}");

            var usnJournal = new UsnJournal(request.Volume);
            var state = usnJournal.GetUsnJournalState();
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
                logger.Info($"PushMasterFileTable request is {request}");
                var stopwatch = Stopwatch.StartNew();
                var usnJournal = new UsnJournal(request.Volume);
                var entries = UsnHelper.SearchMasterFileTable(usnJournal);
                PushUsnEntries(usnJournal, entries);
                stopwatch.Stop();
                logger.Info($"PushMasterFileTable succeeded.{entries.Count()} entries time span {stopwatch.ElapsedMilliseconds}ms");
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
                logger.Info($"PushUsnHistory request is {request}");
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

        public override Task<Empty> MoonitorUsn(Journal request, ServerCallContext context)
        {
            logger.Info($"MoonitorUsn request is {request}");

            var usnJournal = new UsnJournal(request.Volume);
            UsnHelper.MonitorRealTimeUsnJournal(entry => FilterAndPushUsnEntry(usnJournal, entry), usnJournal, _token);
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> CancelMoonitorUsn(Journal request, ServerCallContext context)
        {
            logger.Info($"CancelMoonitorUsn request is {request}");
            _cancelTokenSource.Cancel();
            return Task.FromResult(new Empty());
        }

        private void PushUsnEntries(UsnJournal journal, IEnumerable<UsnEntry> usnEntries)
        {
            var updateRequest = new UpdateRequest();
            string volume = journal.VolumeName.TrimEnd('\\');
            var db_index = FuzzyUtil.VolumeToDbIndex(volume);
            foreach (var entry in usnEntries)
            {
                if (TryGetPathFromFileId(journal, entry.ParentFileReferenceNumber, out var path))
                {
                    var args = new UpdateRequest.Types.UpdateArgs()
                    {
                        DbIdx = db_index,
                        Key = entry.FileReferenceNumber,
                        Val = FuzzyUtil.PackValue(Path.Combine(volume + path, entry.Name), entry.IsFolder),
                        Deleted = false
                    };
                    updateRequest.Args.Add(args);
                }
            }
            _api.UpdateAsync(updateRequest);
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
                Key = entry.FileReferenceNumber,
                Val = Google.Protobuf.ByteString.Empty,
                Deleted = isDelete
            };

            if (isCreate)
            {
                if (TryGetPathFromFileId(journal, entry.ParentFileReferenceNumber, out var path))
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

            updateRequest.Args.Add(args);
            _api.UpdateAsync(updateRequest);
        }


        private bool TryGetPathFromFileId(UsnJournal journal, ulong id, out string path)
        {
            path = string.Empty;

            try
            {
                var key = $"{journal.VolumeName[0]}{id}";

                if (PathDictionary.TryGetValue(key, out path))
                    return true;

                if (journal.TryGetPathFromFileId(id, out path))
                {
                    PathDictionary.Add(key, path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"TryGetPathFromFileId failed. FileId is {id}.");
            }

            return false;
        }
    }
}
