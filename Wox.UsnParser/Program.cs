using Grpc.Core;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Wox.UsnParser
{
    public class Program
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private const string SEARCH_MFT = "-search";
        private const string READ_HISTORY = "-read";
        private const string MONITOR_USN = "-monitor";
        private const string GET_USN = "-getUsn";
        private const string VOLUME = "-volume";
        private const string USN_ID = "-id";

        private static Mode _mode;
        private static string _volume;
        private static ulong _usnid;

        private static int Main(string[]? args)
        {
            try
            {
                InitLog();

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                Server server = new Server();
                server.Services.Add(Usn.BindService(new UsnGrpcService()));
                server.Ports.Add(new ServerPort("127.0.0.1", 38998, ServerCredentials.Insecure));
                server.Start();

                logger.Info($"Usn Grpc service start.");
                if (args?.Length > 1 && ParserArgs(args))
                {
                    return _mode switch
                    {
                        Mode.SEARCH_MFT => SearchMFT(),
                        Mode.READ_HISTORY => ReadHistory(),
                        Mode.MONITOR_USN => MonitorUsn(),
                        Mode.GET_USN_DATA => GetUsnData(),
                        _ => -1,
                    };
                }

                EventWaitHandle waitHandle = new AutoResetEvent(false);
                waitHandle.WaitOne();

            }
            catch (Exception ex)
            {
                logger.Warn($"Unknown exception: {ex.Message}");
            }

            return -1;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            logger.Error((Exception)e.ExceptionObject);
        }

        private static void InitLog()
        {
            var CurrentLogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Wox", "Logs");
            if (!Directory.Exists(CurrentLogDirectory))
            {
                Directory.CreateDirectory(CurrentLogDirectory);
            }

            var configuration = new LoggingConfiguration();
            var fileTarget = new FileTarget()
            {
                Encoding = System.Text.Encoding.UTF8,
                Header = "[Header]",
                Footer = "[Footer]\n",
                FileName = CurrentLogDirectory.Replace(@"\", "/") + "/UsnParser-${shortdate}.log",
                ArchiveAboveSize = 4 * 1024 * 1024,
                Layout = "${longdate}|${level: uppercase = true}|${logger}\n${message}"
            };
#if DEBUG
            configuration.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
#else
            configuration.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
#endif
            LogManager.Configuration = configuration;
        }

        private static int SearchMFT()
        {
            var usnChannel = new Channel("127.0.0.1:38998", ChannelCredentials.Insecure);
            var usn = new Usn.UsnClient(usnChannel);
            usn.PushMasterFileTable(new Journal() { Volume = _volume });

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

        private static bool ParserArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    continue;
                var key = args[i].ToLower();

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
                        logger.Warn($"Unknown argument {key}");
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
