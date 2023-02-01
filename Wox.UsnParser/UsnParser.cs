using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using McMaster.Extensions.CommandLineUtils;
using Wox.UsnParser.Native;

namespace Wox.UsnParser
{
    public static class UsnParser
    {
        public static void MonitorRealTimeUsnJournal(Action<UsnEntry> callback, UsnJournal journal, USN_JOURNAL_DATA_V0 usnState, string keyword, FilterOption filterOption, CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested) return;

                var usnEntries = journal.GetUsnJournalEntries(usnState, Win32Api.USN_REASON_MASK, keyword, filterOption, out usnState);
                foreach(var entry in usnEntries)
                {
                    callback?.Invoke(entry);
                }
            }
        }

        public static IEnumerable<UsnEntry> SearchMasterFileTable(UsnJournal journal, string keyword, FilterOption filterOption)
        {
            var usnEntries = journal.EnumerateUsnEntries(keyword, filterOption);
            return usnEntries;
        }

        public static IEnumerable<UsnEntry> ReadHistoryUsnJournals(IConsole console, UsnJournal journal, ulong usnJournalId, string keyword, FilterOption filterOption)
        {
            var usnReadState = new USN_JOURNAL_DATA_V0
            {
                NextUsn = 0,
                UsnJournalID = usnJournalId
            };

            var usnEntries = journal.ReadUsnEntries(usnReadState, Win32Api.USN_REASON_MASK, keyword, filterOption);
            return usnEntries;
        }
    }
}
