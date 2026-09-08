using System.Linq;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private ulong ContractFixtureYears(string name) =>
            name.EndsWith("_2") ? 100UL : name.EndsWith("_1") ? 10UL : 1UL;

        private void CheckInfernalRoom(RoomInfo room)
        {
            foreach (var name in room.MemberNames)
                if (room.GetServiceYears(name) != ContractFixtureYears(name))
                    Fail("Room contract years were not projected for " + name);
            if (RoomPanel.Instance == null || !RoomPanel.Instance.IsShowing) return;
            foreach (var item in RoomPanel.Instance.GetComponentsInChildren<UI.RoomPanelElement.PlayerListItem>())
            {
                var name = room.MemberNames.FirstOrDefault(member => item.PlayerName == member || item.PlayerName == member + "（旁观）");
                CheckInfernalLabel(item.GetComponent<NamecardAppearance>(), Field<TMP_Text>(item, "_playername"),
                    room.GetNamecard(name), room.GetServiceYears(name));
            }
            WriteResult("infernal-room-" + _clientIndex + ".checked", "Room members and spectators receive contract years and actual infernal artwork/name style.");
        }

        private void CheckInfernalGame(GameInfoSync message)
        {
            var room = message.Info.RoomInfo;
            foreach (var player in message.Info.Players)
                if (player.IsOccupied && room.GetServiceYears(player.PlayerName) != ContractFixtureYears(player.PlayerName))
                    Fail("Game contract years were not projected for " + player.PlayerName);
            foreach (var item in GamePanel.Instance.GetComponentsInChildren<UI.GamePanelElement.PlayerListItem>())
                CheckInfernalLabel(item.GetComponent<NamecardAppearance>(), Field<TMP_Text>(item, "_name"),
                    room.GetNamecard(item.PlayerName), room.GetServiceYears(item.PlayerName));
            WriteResult("infernal-game-" + _clientIndex + ".checked", "Game snapshots and actual namecards/name style verified, spectator=" + message.Info.IsSpectator);
        }

        private void CheckInfernalLabel(NamecardAppearance appearance, TMP_Text label, string equipped, ulong years)
        {
            if (years < 100) return;
            var product = Resources.Load<SO.NamecardCatalogSO>("SO/Shop/NamecardCatalog")
                .GetProduct(InfernalNamecard.Resolve(equipped, years));
            var artwork = Field<Image>(appearance, "_artwork");
            if (!artwork.gameObject.activeSelf || (artwork.sprite != product.Artwork && artwork.sprite != product.Artwork4x3) ||
                label.color != (Color)new Color32(255, 24, 24, 255) || (Color)label.outlineColor != Color.black ||
                Mathf.Abs(label.outlineWidth - 0.2f) > 0.001f)
                Fail("Infernal public appearance did not override the equipped card/name style.");
        }
    }
}