using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wox.Infrastructure.UserSettings;
using Wox.Models;

namespace Wox.Helper
{
    public class QueryFeedLog
    {
        private readonly object _lock = new object();
        private QueryFeed _query;

        public const int TopN = 3;
        public static QueryFeedLog Instance { get; private set; }

        static QueryFeedLog()
        {
            Instance = new QueryFeedLog();
        }

        private QueryFeedLog()
        {
            Init(DateTime.UtcNow);
        }

        public void Init(DateTime createTimeUtc)
        {
            _query = new QueryFeed();
            _query.QueryRecords = new List<QueryRecord>();
            _query.TopN = TopN;
            _query.CurrentDirectory = "";
            _query.FilterMode = "all";
            _query.CreateTime = DateTimeHelper.UtcTimeToUnixEpochMillis(createTimeUtc);
        }

        public void AddRecordRange(List<QueryRecord> records)
        {
            lock (_lock)
            {
                _query.QueryRecords.AddRange(records);
            }
        }

        public void AddRecord(QueryRecord record)
        {
            lock (_lock)
            {
                _query.QueryRecords.Add(record);
            }
        }

        public void WriteLog()
        {
            if (!_query.QueryRecords.Any())
                return;
            var logDir = Path.Combine(DataLocation.DataDirectory(), "QueryFeedLog");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"query-feed-{_query.CreateTime}.log");
            var log = JsonConvert.SerializeObject(_query);
            Debug.WriteLine(log);
            Task.Run(() => File.WriteAllText(logFile, log));

            Init(DateTime.UtcNow);
        }
    }
}
