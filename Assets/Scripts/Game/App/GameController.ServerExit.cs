using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public partial class GameController
    {
        // Startup failures must leave a non-zero exit code for process supervisors. A quit requested from Start, before
        // the player loop runs, exits with code 0 on Linux players, so wait for a couple of frames first.
        private static async UniTaskVoid QuitAfterStartupFailure()
        {
            await UniTask.DelayFrame(2, PlayerLoopTiming.Update);
            Application.Quit(1);
        }
    }
}
