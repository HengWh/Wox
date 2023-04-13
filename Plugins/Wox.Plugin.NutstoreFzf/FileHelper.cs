using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Wox.Plugin.NutstoreFuzzyFinder
{
    public class FileHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle hFile, out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        [StructLayout(LayoutKind.Sequential)]
        struct BY_HANDLE_FILE_INFORMATION
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint dwVolumeSerialNumber;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint nNumberOfLinks;
            public uint nFileIndexHigh;
            public uint nFileIndexLow;
        }

        const int FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        public static ulong GetFileReferenceNumber(string filePath)
        {
            var fileHandle = CreateFile(filePath, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            if (!fileHandle.IsInvalid)
            {
                BY_HANDLE_FILE_INFORMATION fileInfo;
                if (GetFileInformationByHandle(fileHandle, out fileInfo))
                {
                    return ((ulong)fileInfo.nFileIndexHigh << 32) | (ulong)fileInfo.nFileIndexLow;
                }

                fileHandle.Close();
            }

            return 0;
        }

        public static ulong GetFolderReferenceNumber(string folderPath)
        {
            var folderHandle = CreateFile(folderPath, 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);

            if (!folderHandle.IsInvalid)
            {
                BY_HANDLE_FILE_INFORMATION fileInfo;
                if (GetFileInformationByHandle(folderHandle, out fileInfo))
                {
                    return ((ulong)fileInfo.nFileIndexHigh << 32) | (ulong)fileInfo.nFileIndexLow;
                }

                folderHandle.Close();
            }

            return 0;
        }


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern SafeFileHandle CreateFile(
            string lpFileName,
            [MarshalAs(UnmanagedType.U4)] FileAccess dwDesiredAccess,
            [MarshalAs(UnmanagedType.U4)] FileShare dwShareMode,
            IntPtr lpSecurityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);
    }
}
