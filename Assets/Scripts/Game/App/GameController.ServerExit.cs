using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public partial class GameController
    {
        // Startup failures must leave a non-zero exit code for process supervisors. A quit requested from Start, before
        // the player loop runs, exits with code 0 on Linux players, so wait for a couple of frames first.
        private static async UniTaskVoid QuitAfterStartupFailure(string message)
        {
            // Under systemd stdout is a pipe and Unity's log output can still be buffered when the process exits;
            // stderr goes straight to the journal, so the reason is always visible in journalctl.
            try
            {
                Console.Error.WriteLine(message);
                Console.Error.Flush();
                Console.Out.Flush();
            }
            catch (Exception)
            {
                // Reporting must never keep the server from exiting.
            }
            await UniTask.DelayFrame(2, PlayerLoopTiming.Update);
            Application.Quit(1);
        }
    }
}
