using System.Collections;
using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine;

namespace Framework.SO
{
    public class SingletonSOManager : MonoSingleton<SingletonSOManager>
    {
        private Dictionary<System.Type, ScriptableObject> _loadedSOFiles = new();

        private T LoadSOFiles<T>(string filename) where T : ScriptableObject
        {
            string path = "SO/" + filename;

            // Use Resources.load
            T file = Resources.Load(path) as T;
            _loadedSOFiles.Add(typeof(T), file);
            return file;
        }

        public T GetSOFile<T>(string filename) where T : ScriptableObject
        {
            if (_loadedSOFiles.TryGetValue(typeof(T), out ScriptableObject so))
            {
                return so as T;
            }
            else
                return LoadSOFiles<T>(filename);
        }
    }
}