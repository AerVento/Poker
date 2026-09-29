using Framework.UI;

namespace Game.UI
{
    public static class CurrencyFeedback
    {
        public static void ShowInsufficientFunds()
        {
            if (!UIManager.Available) return;
            var message = MsgBox.Create("InsufficientFunds");
            message.Type = MsgboxType.Confirm;
            message.Title = "购买失败";
            message.Message = "余额不足。";
            message.ConfirmButtonText = "知道了";
        }
    }
}
