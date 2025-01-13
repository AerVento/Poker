using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Framework.Serialization;



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
        /// The directory where saves the configuration.
        /// </summary>
        public const string CONFIG_SAVE_PATH = "Configs/";


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
            string json = File.ReadAllText(CONFIG_SAVE_PATH + attribute.Path);
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
            var path = CONFIG_SAVE_PATH + attribute.Path;
            
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
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
            string path = CONFIG_SAVE_PATH + attribute.Path;
            
            string directoryPath = Path.GetDirectoryName(path);
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            string json = JsonConvert.SerializeObject(config, _settings);
            File.WriteAllText(path, json);
        }

        private static ConfigAttribute GetAttribute(Type type)
        {
            ConfigAttribute attribute = type.GetCustomAttribute<ConfigAttribute>();
            if (attribute != null)
                return attribute;
            throw new ArgumentException($"Type {type} has no attribute of ConfigAttribute on it. It cannot be loaded with ConfigManager.", type.ToString());
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

               string path = CONFIG_SAVE_PATH + attribute.Path;

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
                File.WriteAllText(path, json);
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
            if (Directory.Exists(CONFIG_SAVE_PATH))
            {
                Directory.Delete(CONFIG_SAVE_PATH, true);
                Debug.Log("Removed the directory at " + CONFIG_SAVE_PATH);
            }
        }
#endif
    }
}

