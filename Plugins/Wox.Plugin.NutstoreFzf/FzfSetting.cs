using Newtonsoft.Json;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class FzfSetting
    {
        public int Version { get; set; }
        public int MaxSearchCount { get; set; } = 60;

        public int BaseScore { get; set; } = 2000;

        public List<DeviceUsnState> UsnStates { get; set; } = new List<DeviceUsnState>();

        public void AddOrUpdateUsnState(DeviceUsnState state)
        {
            if (TryGetUsnState(state.Volume, out var oldState))
            {
                oldState.Update(state);
            }
            else
            {
                UsnStates.Add(state);
            }
        }

        public bool TryGetUsnState(string volume, out DeviceUsnState oldState)
        {
            oldState = UsnStates.FirstOrDefault(p => p.Volume.Equals(volume, StringComparison.OrdinalIgnoreCase));
            return oldState != null;
        }

        public IReadOnlyCollection<DeviceUsnState> GetUsnStates()
        {
            return UsnStates.AsReadOnly();
        }
    }

    public class DeviceUsnState
    {
        public string Volume { get; set; }
        public ulong UsnJournalId { get; set; }
        public ulong USN { get; set; }

        public void Update(DeviceUsnState state)
        {
            Volume = state.Volume;
            UsnJournalId = state.UsnJournalId;
            USN = state.USN;
        }
    }
}
