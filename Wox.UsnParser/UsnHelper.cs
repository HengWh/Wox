using Wox.UsnParser.Native;

namespace Wox.UsnParser
{
    public static class UsnHelper
    {
        public static void MonitorRealTimeUsnJournal(Action<UsnEntry> callback, UsnJournal journal, CancellationToken token)
        {
            var journalData = journal.GetUsnJournalState();
            MonitorRealTimeUsnJournal(callback, journal, journalData, token);
        }

        public static void MonitorRealTimeUsnJournal(Action<UsnEntry> callback, UsnJournal journal, USN_JOURNAL_DATA_V0 usnState, CancellationToken token)
        {
            while (true)
            {
                if (token.IsCancellationRequested) return;

                var usnEntries = journal.GetUsnJournalEntries(usnState, Win32Api.USN_REASON_MASK, null, FilterOption.All, out usnState);
                foreach (var entry in usnEntries)
                {
                    callback?.Invoke(entry);
                }
            }
        }

        public static IEnumerable<UsnEntry> SearchMasterFileTable(UsnJournal journal)
        {
            var usnEntries = journal.EnumerateUsnEntries();
            return usnEntries;
        }

        public static IEnumerable<UsnEntry> ReadHistoryUsnJournals(UsnJournal journal, ulong nextUsn)
        {
            var journalData = journal.GetUsnJournalState();
            var usnReadState = new USN_JOURNAL_DATA_V0
            {
                NextUsn = Convert.ToInt64(nextUsn),
                UsnJournalID = journalData.UsnJournalID
            };

            var usnEntries = journal.ReadUsnEntries(usnReadState, Win32Api.USN_REASON_MASK, "*", FilterOption.All);
            return usnEntries;
        }
    }
}
