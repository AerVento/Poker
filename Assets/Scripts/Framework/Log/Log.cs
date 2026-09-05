using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Framework.Log
{
    public enum LogLevel
    {
        Info,
        Warn, 
        Error
    }
    public static class Log
    {
        private static string _path;
        private static FileStream _stream;
        public static string Path
        {
            get => _path;
            set
            {
                if (_stream != null && value == _path)
                    return;
                var dir = System.IO.Path.GetDirectoryName(value);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var stream = File.Open(value, FileMode.Append, FileAccess.Write, FileShare.Read);
                _stream?.Close();
                _stream = stream;
                _path = value;
            }
        }
        static Log()
        {
            DateTime time = DateTime.Now;
            try
            {
                // Clients launched together must never contend for the same log file.
                Path = $"./logs/log-[{time:yyyy-MM-dd}][{time:HH-mm-ss}]-{Guid.NewGuid():N}.txt";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"File logging is unavailable: {exception.Message}");
            }
        }

        private static void WriteString(string msg)
        {
            try
            {
                _stream?.Write(Encoding.UTF8.GetBytes(msg));
            }
            catch (Exception exception) when (exception is IOException || exception is ObjectDisposedException)
            {
                _stream = null;
                Debug.LogWarning($"File logging stopped: {exception.Message}");
            }
#if UNITY_EDITOR
            Debug.Log(msg);
#endif
        }

        public static void Write(string msg, LogLevel level = LogLevel.Info)
        {
            string label = string.Empty;
            switch (level)
            {
                case LogLevel.Info:
                    label = "INFO"; break;
                case LogLevel.Warn:
                    label = "WARN"; break;
                case LogLevel.Error:
                    label = "ERROR"; break;
                default:
                    break;
            }
            DateTime time = DateTime.Now;
            WriteString($"[{time}][{label}]:{msg}\n");
        }
        public static void WriteInfo(string msg)
        {
            DateTime time = DateTime.Now;
            WriteString($"[{time}][INFO]:{msg}\n");
        }
        public static void WriteWarn(string msg)
        {
            DateTime time = DateTime.Now;
            WriteString($"[{time}][WARN]:{msg}\n");
        }
        public static void WriteError(string msg)
        {
            DateTime time = DateTime.Now;
            WriteString($"[{time}][ERROR]:{msg}\n");
        }

        public static void Close()
        {
            if(_stream != null)
            {
                _stream.Close();
                _stream = null;
            }
        }
    }
}
