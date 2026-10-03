using System.Linq;
using System.Collections.Generic;
using Framework.UI;
using Game.Network;
using Game.UI;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // Remote namecard artwork shows the frame and text-safe guides; names are in Configs/Items/Items.json, art addresses in Configs/Items/Namecards.json and prices in Configs/Shop/Shop.json.
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
            // Offline preview: show the catalog from Configs as if the server had sent it.
            if (!ClientCatalog.IsAvailable)
                ClientCatalog.Apply(ClientCatalog.ToMessage(CatalogConfiguration.Load()));
            var panel = UIManager.Instance.PushPanel<ShopPanel>();
            panel.SelectTab(ShopTab.Namecards);
            var owned = new HashSet<string> { NamecardDefinition.DefaultItemId, "namecard-galaxy", "namecard-neon" };
            uint previewBalance = 500000;
            string equipped = string.Empty;
            System.Action refresh = () =>
            {
                panel.SetInventory("Poker Player", new InventorySnapshot(owned.Select(id => new ItemStack(id, 1)),
                    dollars: previewBalance, equippedNamecard: equipped, canAct: true, canEquip: true));
                panel.SetFeedback("界面预览 · 示例账户与库存，仅在本次预览中有效");
            };
            panel.PurchaseRequested += id =>
            {
                var price = ClientCatalog.FindOffer(id)?.Price ?? uint.MaxValue;
                if (owned.Contains(id) || previewBalance < price)
                    return;
                previewBalance -= price;
                owned.Add(id);
                refresh();
            };
            panel.EquipRequested += id =>
            {
                if (!owned.Contains(id))
                    return;
                equipped = id == NamecardDefinition.DefaultItemId ? string.Empty : id;
                refresh();
            };
            refresh();
        }
    }
}
