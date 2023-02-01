﻿using System;

namespace Wox.UsnParser.Native
{
    [Flags]
    public enum UsnReason : uint
    {
        NONE = 0x00000000,

        // From winioctl.h
        DATA_OVERWRITE = 0x00000001,
        DATA_EXTEND = 0x00000002,
        DATA_TRUNCATION = 0x00000004,
        NAMED_DATA_OVERWRITE = 0x00000010,
        NAMED_DATA_EXTEND = 0x00000020,
        NAMED_DATA_TRUNCATION = 0x00000040,
        FILE_CREATE = 0x00000100,
        FILE_DELETE = 0x00000200,
        EA_CHANGE = 0x00000400,
        SECURITY_CHANGE = 0x00000800,
        RENAME_OLD_NAME = 0x00001000,
        RENAME_NEW_NAME = 0x00002000,
        INDEXABLE_CHANGE = 0x00004000,
        BASIC_INFO_CHANGE = 0x00008000,
        HARD_LINK_CHANGE = 0x00010000,
        COMPRESSION_CHANGE = 0x00020000,
        ENCRYPTION_CHANGE = 0x00040000,
        OBJECT_ID_CHANGE = 0x00080000,
        REPARSE_POINT_CHANGE = 0x00100000,
        STREAM_CHANGE = 0x00200000,
        CLOSE = 0x80000000,
    }
}
