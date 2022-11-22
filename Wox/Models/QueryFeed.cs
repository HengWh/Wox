using Newtonsoft.Json;
using System.Collections.Generic;

namespace Wox.Models
{
    public class QueryFeed
    {
        [JsonProperty("n")]
        public int TopN { get; set; }

        [JsonProperty("pwd")]
        public string CurrentDirectory { get; set; }

        [JsonProperty("filter_mode")]
        public string FilterMode { get; set; }

        /// <summary>
        /// Unix timestamp milliseconds
        /// </summary>
        [JsonProperty("ctime")]
        public long CreateTime { get; set; }

        [JsonProperty("query_records")]
        public List<QueryRecord> QueryRecords { get; set; }
    }

    public class QueryRecord
    {
        [JsonProperty("query")]
        public string Query { get; set; }

        [JsonProperty("ctime")]
        public long CreateTime { get; set; }

        [JsonProperty("top_n")]
        public List<string> ResultTopN { get; set; }

        [JsonProperty("selected_idx")]
        public int SelectedIndex { get; set; }

        [JsonProperty("selected_str")]
        public string SelectedStr { get; set; }

        [JsonProperty("final")]
        public bool Final { get; set; }

        [JsonProperty("final_elapsed")]
        public long FinalElapsed { get; set; }
    }
}
