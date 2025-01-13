using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Framework.Singleton
{
    public class Singleton<T> where T : class
    {
        protected static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    Type t = typeof(T);
                    // the constructor with no arguments
                    var info = t.GetConstructors().First((info) => info.GetParameters().Length == 0);
                    _instance = (T)info.Invoke(new object[0]);
                }
                return _instance;
            }
        }
        protected Singleton() { }
    }
}

