using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    internal class FzfSetting
    {
        public int MaxSearchCount { get; set; } = 60;
        public int BaseScore { get; set; } = 100;
        public List<DeviceUsnState> UsnStates { get; set; }

    }

    internal class DeviceUsnState
    {
        public string Volume { get; set; }
        public string UsnJournalId { get; set; }
        public ulong USN { get;set; } 
    }
}
