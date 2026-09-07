using System;
using System.Collections;
using System.IO;
using System.Linq;
using Framework.UI;
using Game.Network.Accounts;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testShop;
        private bool _shopGameChecked;
        private bool _shopRoomChecked;

        private IEnumerator ShopWait(ShopPanel panel)
        {
            var until = Time.realtimeSinceStartup + 15f;
            while (Field<NamecardShopRequest>(panel, "_pending").RequestId != 0 &&
                Time.realtimeSinceStartup < until)
                yield return null;
            if (!Field<bool>(panel, "_hasAccount") || !Field<bool>(panel, "_canAct"))
                Fail("Shop failed to load: " + Field<TMP_Text>(panel, "_feedback").text);
        }

        private IEnumerator CheckShopUI(bool purchase)
        {
            var until = Time.realtimeSinceStartup + 15f;
            while ((!_authenticated || RoomListPanel.Instance == null || !RoomListPanel.Instance.IsShowing) &&
                Time.realtimeSinceStartup < until)
                yield return null;
            if (!_authenticated) { Fail("Shop preflight could not authenticate."); yield break; }
            UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_shop");
            if (_finishing) yield break;
            var panel = ShopPanel.Instance;
            if (panel == null || !panel.IsShowing) { Fail("Shop entry did not open."); yield break; }
            yield return ShopWait(panel);
            if (_finishing) yield break;
            var cards = Field<ShopItemCard[]>(panel, "_cards");
            if (panel.PageCount != 3 || cards.Length != 4)
                Fail("Shop pagination was not 4 per page.");
            if (purchase)
            {
                if (Field<TMP_Text>(cards[0], "_actionLabel").text != "已装备")
                    Fail("New account did not equip default.");
                Click(cards[1], "_action");
                var box = FindObjectsOfType<MsgBox>().FirstOrDefault(b => b.Title == "收藏这张名片");
                if (box == null || !box.Message.Contains("6,000")) { Fail("Purchase confirmation missing authoritative quote."); yield break; }
                Click(box, "_cancelButton");
                if (GameController.Instance.State.Chips != 10000) Fail("Cancel changed balance.");
                Click(cards[1], "_action");
                box = FindObjectsOfType<MsgBox>().First(b => b.Title == "收藏这张名片");
                Click(box, "_confirmButton");
                yield return ShopWait(panel);
                if (_finishing) yield break;
                if (GameController.Instance.State.Chips != 4000 || Field<TMP_Text>(cards[1], "_actionLabel").text != "装备")
                    Fail("Purchase did not debit once and become Equip.");
                Click(cards[1], "_action");
                yield return ShopWait(panel);
                if (Field<string>(panel, "_equippedId") != "galaxy")
                    Fail("Equip did not persist.");
                Click(cards[0], "_action");
                yield return ShopWait(panel);
                if (Field<string>(panel, "_equippedId") != "")
                    Fail("Default switch failed.");
                if (_clientIndex != 2)
                {
                    Click(cards[1], "_action");
                    yield return ShopWait(panel);
                }
            }
            else if (Field<string>(panel, "_equippedId") != (_clientIndex == 2 ? "" : "galaxy"))
                Fail("Reopened shop lost equipped appearance.");
            Click(panel, "_next");
            Click(panel, "_next");
            if (panel.CurrentPage != 2 || Field<Button>(panel, "_next").interactable)
                Fail("Last page boundary failed.");
            Click(panel, "_back");
            if (Framework.UI.UIManager.Instance.TopPanelIdentifier != nameof(TitlePanel))
            {
                Fail("Feature back did not return to title.");
                yield break;
            }
            Click(TitlePanel.Instance, "_start");
            WriteResult("shop-" + _clientIndex + "-" + (purchase ? "purchase" : "reopen") + ".checked",
                "Actual title entry and back, quote, cancel, permanent purchase, free equip/default, pagination and authoritative balance.");
        }

        private void CheckShopRoom(RoomInfo room)
        {
            foreach (var name in room.MemberNames)
            {
                var expected = name.EndsWith("_2") ? "" : "galaxy";
                if (room.GetNamecard(name) != expected)
                    Fail("Room public namecard differs for " + name);
            }
            if (RoomPanel.Instance != null && RoomPanel.Instance.IsShowing)
            {
                foreach (var item in RoomPanel.Instance.GetComponentsInChildren<UI.RoomPanelElement.PlayerListItem>())
                {
                    var expected = item.PlayerName.Contains("_2") ? "" : "galaxy";
                    var appearance = item.GetComponent<NamecardAppearance>();
                    var art = Field<Image>(appearance, "_artwork");
                    if (art.gameObject.activeSelf != (expected != "") ||
                        expected != "" && art.sprite != Resources.Load<Game.SO.NamecardCatalogSO>("SO/Shop/NamecardCatalog").GetProduct(expected).Artwork)
                        Fail("Waiting-room namecard artwork differs.");
                }
            }
            _shopRoomChecked = true;
        }

        private void CheckShopGame(GameInfoSync message)
        {
            var info = message.Info;
            foreach (var player in info.Players.Where(p => p.IsOccupied))
            {
                var expected = player.PlayerName.EndsWith("_2") ? "" : "galaxy";
                if (info.RoomInfo.GetNamecard(player.PlayerName) != expected)
                    Fail("Game projection lost public namecard for " + player.PlayerName);
            }
            foreach (var item in GamePanel.Instance.GetComponentsInChildren<UI.GamePanelElement.PlayerListItem>())
            {
                var expected = item.PlayerName.EndsWith("_2") ? "" : "galaxy";
                var appearance = item.GetComponent<NamecardAppearance>();
                var art = Field<Image>(appearance, "_artwork");
                var border = Field<Image>(appearance, "_outline");
                if (art.gameObject.activeSelf != (expected != "") ||
                    expected != "" && (art.sprite != Resources.Load<Game.SO.NamecardCatalogSO>("SO/Shop/NamecardCatalog").GetProduct(expected).Artwork || border.gameObject.activeSelf != item.IsOperating))
                    Fail("Table artwork or operating outline differs.");
            }
            if (!_shopGameChecked)
            {
                _shopGameChecked = true;
                WriteResult("shop-public-" + _clientIndex + ".checked",
                    "Public room/game appearance and actual player art verified, spectator=" + info.IsSpectator);
            }
        }
    }
}
