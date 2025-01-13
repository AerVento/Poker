using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Framework.Configs
{
    /// <summary>
    /// The attribute used to easily set up config file path.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class ConfigAttribute : Attribute
    {
        /// <summary>
        /// The file path of configuartion. It is a relative path to <see cref="ConfigManager.CONFIG_SAVE_PATH"/>.
        /// </summary>
        public string Path { get; set; } = string.Empty;
    }
}