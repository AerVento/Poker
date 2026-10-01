using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    /// <summary>Blocks an outdated WebGL page with a refresh-only message box.</summary>
    public static class PageUpdatePrompt
    {
        private const string Identifier = "PageUpdate";
        private static bool _shown;

        /// <summary>True when the page is current or cannot be checked; otherwise shows the prompt and returns false.</summary>
        public static async UniTask<bool> EnsureCurrentAsync(CancellationToken cancellationToken = default)
        {
            if (_shown) return false;
            var deployed = await PageVersion.FindNewerAsync(cancellationToken);
            if (deployed == null) return true;
            Show(deployed);
            return false;
        }

        private static void Show(string deployed)
        {
            if (_shown) return;
            _shown = true;
            MsgBox.Create(Identifier, box =>
            {
                box.Title = "游戏已更新";
                box.Message = "请刷新页面后继续。";
                box.Type = MsgboxType.Confirm;
                box.ShowCloseButton = false;
                box.ConfirmButtonText = "刷新";
                box.OnConfirm += () => PageVersion.Reload(deployed);
                // Esc closes message boxes; the next login or lobby check asks again.
                box.OnCancel += () => _shown = false;
            });
        }
    }
}
