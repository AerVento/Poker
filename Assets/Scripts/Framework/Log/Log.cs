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
                _path = value;
                var dir = System.IO.Path.GetDirectoryName(_path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if( _stream != null )
                    _stream.Close();
                _stream = File.Open(_path, FileMode.OpenOrCreate, FileAccess.Write);
            }
        }
        static Log()
        {
            DateTime time = DateTime.Now;
            Path = $"./logs/log-[{time.Year}-{time.Month}-{time.Day}][{time.Hour}-{time.Minute}-{time.Second}].txt";
        }

        private static void WriteString(string msg)
        {
            _stream.Write(Encoding.UTF8.GetBytes(msg));
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
