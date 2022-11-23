using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure
{
    public static class Helper
    {
        private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// http://www.yinwang.org/blog-cn/2015/11/21/programming-philosophy
        /// </summary>
        public static T NonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
            else
            {
                return obj;
            }
        }

        public static void RequireNonNull<T>(this T obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }
        }

        public static void ValidateDataDirectory(string bundledDataDirectory, string dataDirectory)
        {
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            foreach (var bundledDataPath in Directory.GetFiles(bundledDataDirectory))
            {
                var data = Path.GetFileName(bundledDataPath);
                var dataPath = Path.Combine(dataDirectory, data.NonNull());
                if (!File.Exists(dataPath))
                {
                    File.Copy(bundledDataPath, dataPath);
                }
                else
                {
                    var time1 = new FileInfo(bundledDataPath).LastWriteTimeUtc;
                    var time2 = new FileInfo(dataPath).LastWriteTimeUtc;
                    if (time1 != time2)
                    {
                        File.Copy(bundledDataPath, dataPath, true);
                    }
                }
            }
        }

        public static void ValidateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string Formatted<T>(this T t)
        {
            var formatted = JsonConvert.SerializeObject(
               t,
               Formatting.Indented,
               new StringEnumConverter()
           );
            return formatted;
        }

        public static bool OpenInShell(string path, string arguments = null, string workingDir = null, ShellRunAsType runAs = ShellRunAsType.None, bool runWithHiddenWindow = false)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = path;
                process.StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? string.Empty : workingDir;
                process.StartInfo.Arguments = string.IsNullOrWhiteSpace(arguments) ? string.Empty : arguments;
                process.StartInfo.WindowStyle = runWithHiddenWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
                process.StartInfo.UseShellExecute = true;

                if (runAs == ShellRunAsType.Administrator)
                {
                    process.StartInfo.Verb = "RunAs";
                }
                else if (runAs == ShellRunAsType.OtherUser)
                {
                    process.StartInfo.Verb = "RunAsUser";
                }

                try
                {
                    process.Start();
                    return true;
                }
                catch (Win32Exception ex)
                {
                    Logger.WoxError($"Unable to open {path}: {ex.Message}", ex);
                    return false;
                }
            }

        }
        public enum ShellRunAsType
        {
            None,
            Administrator,
            OtherUser,
        }
    }
}
