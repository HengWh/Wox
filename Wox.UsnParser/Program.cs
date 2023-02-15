using Newtonsoft.Json;
using NLog;
using Wox.Infrastructure.Logger;

namespace Wox.UsnParser
{
    public class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private const string SEARCH_MFT = "-search";
        private const string READ_HISTORY = "-read";
        private const string MONITOR_USN = "-monitor";
        private const string GET_USN = "-getUsn";
        private const string VOLUME = "-volume";
        private const string USN_ID = "-id";

        private static Mode _mode;
        private static string _volume;
        private static ulong _usnid;

        private int Main(string[]? args)
        {
            try
            {
                if (args == null || !ParserArgs(args))
                {
                    Logger.WoxWarn($"Arguments is invalid. {args}");
                    return -1;
                }

                return _mode switch
                {
                    Mode.SEARCH_MFT => SearchMFT(),
                    Mode.READ_HISTORY => ReadHistory(),
                    Mode.MONITOR_USN => MonitorUsn(),
                    Mode.GET_USN_DATA => GetUsnData(),
                    _ => -1,
                };
            }
            catch (Exception ex)
            {
                Logger.WoxWarn($"Unknown exception: {ex.Message}");
            }

            return -1;
        }

        private static int SearchMFT()
        {
            var driveInfo = new DriveInfo(_volume);
            using var journal = new UsnJournal(driveInfo);
            var entries = UsnHelper.SearchMasterFileTable(journal);

            return 0;
        }

        private static int ReadHistory()
        {
            var driveInfo = new DriveInfo(_volume);
            using var journal = new UsnJournal(driveInfo);
            var entries = UsnHelper.ReadHistoryUsnJournals(journal, _usnid);
            return 0;
        }

        private static int MonitorUsn()
        {
            var driveInfo = new DriveInfo(_volume);
            using var journal = new UsnJournal(driveInfo);
            var usnState = journal.GetUsnJournalState();
            var cts = new CancellationTokenSource();
            var token = cts.Token;
            UsnHelper.MonitorRealTimeUsnJournal(null, journal, usnState, token);
            return 0;
        }

        private static int GetUsnData()
        {
            var driveInfo = new DriveInfo(_volume);
            using var journal = new UsnJournal(driveInfo);
            var usnJournalData = journal.GetUsnJournalState();
            Console.WriteLine(JsonConvert.SerializeObject(usnJournalData));
            return 0;
        }


        //
        private static bool ParserArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    continue;
                var key = args[i].TrimStart('-').ToLower();

                switch (key)
                {
                    case SEARCH_MFT:
                        _mode = Mode.SEARCH_MFT;
                        break;
                    case READ_HISTORY:
                        _mode = Mode.READ_HISTORY;
                        break;
                    case MONITOR_USN:
                        _mode = Mode.MONITOR_USN;
                        break;
                    case GET_USN:
                        _mode = Mode.GET_USN_DATA;
                        break;
                    case VOLUME:
                        _volume = args[i + 1];
                        break;
                    case USN_ID:
                        _usnid = Convert.ToUInt64(args[i + 1]);
                        break;
                    default:
                        Logger.WoxWarn($"Unknown argument {key}");
                        break;
                }
            }
            if (_mode == Mode.UNKNOWN || string.IsNullOrEmpty(_volume))
                return false;
            return true;
        }
    }

    public enum Mode
    {
        UNKNOWN,
        SEARCH_MFT,
        READ_HISTORY,
        MONITOR_USN,
        GET_USN_DATA
    }
}
