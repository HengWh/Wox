using System;

namespace Wox.Helper
{
    public static class DateTimeHelper
    {
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long UtcTimeToUnixEpochMillis(DateTime utcDateTime)
        {
            return (long)(utcDateTime - UnixEpoch).TotalMilliseconds;
        }
    }
}
