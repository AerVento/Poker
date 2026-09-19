using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Framework.Serialization;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.Configs
{
    /// <summary>
    /// A manager to easily load the data structure with <see cref="ConfigAttribute"/> attribute.
    /// </summary>
    public static class ConfigManager 
    {
        /// <summary>
        /// The buffer to contain loaded configs.
        /// </summary>
        private static Dictionary<Type, object> _buffer = new Dictionary<Type, object>();

        /// <summary>
        /// A universal serialize setting for unity projects.
        /// </summary>
        private static JsonSerializerSettings _settings = new JsonSerializerSettings()
        {
            Converters =
            {
                new Vector2Converter(),
                new Vector3Converter(),
                new Vector2IntConverter(),
                new Vector3IntConverter(),
                new RectConverter(),
            },
            Formatting = Formatting.Indented,
        };

        /// <summary>
        /// The project/build-relative root used by every default configuration path.
        /// </summary>
        public const string CONFIG_SAVE_PATH = "Configs";

        public static string RootDirectory => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", CONFIG_SAVE_PATH));

        public static string GetDirectory(params string[] segments)
        {
            var path = RootDirectory;
            foreach (var segment in segments ?? Array.Empty<string>())
                path = ResolveChild(path, segment);
            return path;
        }

        public static string GetPath(params string[] segments) => GetDirectory(segments);

        public static bool FileExists(params string[] segments) => File.Exists(GetPath(segments));

        public static string ReadAllText(params string[] segments) => File.ReadAllText(GetPath(segments));

        public static string ReadAllTextAtPath(string path) => File.ReadAllText(Path.GetFullPath(path));

        public static void WriteAllText(string text, params string[] segments)
        {
            var path = GetPath(segments);
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text);
        }

        public static void WriteAllTextAtPath(string path, string text)
        {
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text);
        }

        public static void WriteAllTextAtPath(string path, string text, Encoding encoding)
        {
            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, encoding);
        }

        public static void CopyFileAtPath(string source, string destination, bool overwrite)
        {
            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);
            var directory = Path.GetDirectoryName(destination);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, destination, overwrite);
        }

        /// <summary>
        /// Load a configuration.
        /// </summary>
        /// <typeparam name="T">The type of data structure.</typeparam>
        /// <returns>The data structure loaded.</returns>
        /// <exception cref="ArgumentException">Thrown if the type T has no attribute of ConfigAttribute on it.</exception>
        /// <exception cref="FileNotFoundException">Thrown if cannot find the file at given path..</exception>
        public static T Load<T>()
        {
            Type type = typeof(T);

            if(_buffer.ContainsKey(type))
                return (T)_buffer[type];

            var attribute = GetAttribute(type);
            string json = ReadAllText(attribute.Path);
            T result = JsonConvert.DeserializeObject<T>(json, _settings);
            
            if(result == null)
                Debug.LogError($"Cannot deserialize the json text to {type}. The raw text is below: \n" + json);
            return result;
        }

        /// <summary>
        /// Load a configuration. If the configuration doesn't exist, return default value. Otherwise, return the loaded configuration.
        /// </summary>
        /// <typeparam name="T">The type of data structure.</typeparam>
        /// <param name="defaultValue">If the configuration doesn't exists, use this value as return value.</param>
        /// <param name="result">The output result.</param>
        /// <returns>If the configuration exists.</returns>
        /// <exception cref="ArgumentException">Thrown if the type T has no attribute of ConfigAttribute on it.</exception>
        public static bool Load<T>(T defaultValue, out T result)
        {
            Type type = typeof(T);

            if (_buffer.ContainsKey(type))
            {
                result = (T)_buffer[type];
                return true;
            }

            var attribute = GetAttribute(type);
            var path = GetPath(attribute.Path);
            
            if (File.Exists(path))
            {
                string json = ReadAllTextAtPath(path);
                result = JsonConvert.DeserializeObject<T>(json);
                
                if (result == null)
                    Debug.LogError($"Cannot deserialize the json text to {type}. The raw text is below: \n" + json);
                return result != null;
            }
            
            result = defaultValue;
            return false;
        }

        /// <summary>
        /// Save a configuration.
        /// </summary>
        /// <typeparam name="T">The type of data structure.</typeparam>
        /// <param name="config">The data structure need to be saved.</param>
        /// <exception cref="ArgumentException">Thrown if the type T has no attribute of ConfigAttribute on it.</exception>
        public static void Save<T>(T config)
        {
            var attribute = GetAttribute(typeof(T));
            string path = GetPath(attribute.Path);
            
            string directoryPath = Path.GetDirectoryName(path);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string json = JsonConvert.SerializeObject(config, _settings);
            WriteAllTextAtPath(path, json);
        }

        private static ConfigAttribute GetAttribute(Type type)
        {
            ConfigAttribute attribute = type.GetCustomAttribute<ConfigAttribute>();
            if (attribute != null)
                return attribute;
            throw new ArgumentException($"Type {type} has no attribute of ConfigAttribute on it. It cannot be loaded with ConfigManager.", type.ToString());
        }

        private static string ResolveChild(string parent, string segment)
        {
            if (string.IsNullOrWhiteSpace(segment) || Path.IsPathRooted(segment))
                throw new ArgumentException("Configuration path segments must be non-empty and relative.", nameof(segment));
            var root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(Path.Combine(root, segment));
            if (!child.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Configuration paths must stay inside the Configs directory.", nameof(segment));
            return child;
        }

#if UNITY_EDITOR
        /// <summary>
        /// For all type in assembly of <see cref="ConfigAttribute"/>, create default value of config file at given path.
        /// </summary>
        [MenuItem("Utils/Configs/Create Config File")]
        public static void CreateConfigFiles()
        {
            Assembly assembly = Assembly.GetAssembly(typeof(ConfigAttribute));
            
            foreach(var type in assembly.GetTypes())
            {
                var attribute = type.GetCustomAttribute<ConfigAttribute>();
                
                if (attribute == null)
                    continue;

                string path = GetPath(attribute.Path);

                if (File.Exists(path))
                {
                    Debug.Log($"Type {type} already has a config file at {path}. This type will be skipped.");
                    continue;
                }

                // Create parent directory
                string directoryPath = Path.GetDirectoryName(path);
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                // Create a instance with default constructor
                object instance = Activator.CreateInstance(type);
                
                // Serialize
                string json = JsonConvert.SerializeObject(instance, _settings);
                WriteAllTextAtPath(path, json);
                Debug.Log($"Successfully create config file for type {type} at path {path}.");
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Remove all config files.
        /// </summary>
        [MenuItem("Utils/Configs/Remove All Config File")]
        public static void ClearConfigFiles()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, true);
                Debug.Log("Removed the directory at " + RootDirectory);
            }
        }
#endif
    }
}

