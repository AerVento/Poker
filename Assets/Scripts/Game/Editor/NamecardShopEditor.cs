using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // Remote namecard artwork shows the frame and text-safe guides; names and prices are in Configs/Shop/Namecards.json.
    [CustomEditor(typeof(Game.Content.NamecardArtworkSO))]
    public sealed class NamecardArtworkEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            NamecardArtValidation.DrawPreview((Game.Content.NamecardArtworkSO)target);
        }
    }

    [InitializeOnLoad]
    public static class NamecardShopPreview
    {
        private const string PendingKey = "Poker.NamecardShopPreview.Pending";

        static NamecardShopPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
                {
                    SessionState.SetBool(PendingKey, false);
                    EditorApplication.delayCall += ShowPreview;
                }
            };
        }

        [MenuItem("Poker/UI/Preview Namecard Shop")]
        public static void OpenPreview()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingKey, true);
                EditorApplication.EnterPlaymode();
                return;
            }
            ShowPreview();
        }

        public static void ShowPreview()
        {
            if (!EditorApplication.isPlaying || !UIManager.Available)
                return;
            if (UIManager.Instance.ContainsPanel<ShopPanel>())
                UIManager.Instance.PopPanel<ShopPanel>();
            // Offline preview: show the products from Configs/Shop as if the server had sent them.
            if (!ClientShopCatalog.IsAvailable)
                ClientShopCatalog.Apply(ClientShopCatalog.ToMessage(ShopConfiguration.Load()));
            var panel = UIManager.Instance.PushPanel<ShopPanel>();
            panel.SelectTab(ShopTab.Namecards);
            var owned = new HashSet<string> { "galaxy", "neon" };
            uint previewBalance = 500000;
            string equipped = string.Empty;
            System.Action refresh = () =>
            {
                panel.SetAccountView("Poker Player", previewBalance, owned, equipped, true);
                panel.SetFeedback("界面预览 · 示例账户与库存，仅在本次预览中有效");
            };
            panel.PurchaseRequested += product =>
            {
                if (product.IsDefault || owned.Contains(product.Id) || previewBalance < product.Price)
                    return;
                previewBalance -= product.Price;
                owned.Add(product.Id);
                refresh();
            };
            panel.EquipRequested += id =>
            {
                if (!string.IsNullOrEmpty(id) && !owned.Contains(id))
                    return;
                equipped = id;
                refresh();
            };
            refresh();
        }
    }
}
