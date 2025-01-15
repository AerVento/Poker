using Framework.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Tests
{
    public class LogTester : MonoBehaviour
    {
        private void Start()
        {
            Test();
        }
        public void Test()
        {
            Log.WriteInfo("Hello");
        }
    }
}
