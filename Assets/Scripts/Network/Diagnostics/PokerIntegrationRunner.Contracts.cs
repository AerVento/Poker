using System.Collections;
using System.Linq;
using Framework.UI;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testContracts;

        private IEnumerator CheckContractsUI(bool purchase)
        {
            var until = Time.realtimeSinceStartup + 20f;
            while ((!_authenticated || RoomListPanel.Instance == null || !RoomListPanel.Instance.IsShowing) &&
                Time.realtimeSinceStartup < until) yield return null;
            if (!_authenticated) { Fail("Contract preflight could not authenticate."); yield break; }
            UIManager.Instance.ResetPanels<TitlePanel>();
            Click(TitlePanel.Instance, "_shop");
            var panel = ShopPanel.Instance;
            if (panel == null || !panel.IsShowing) { Fail("Contract shop did not open."); yield break; }
            yield return ShopWait(panel);
            if (_finishing) yield break;
            var view = panel.ItemsView;
            var ids = new[] { "devil-contract", "archdevil-contract", "satan-contract" };
            var rewards = new uint[] { 1000, 10000, 100000 };
            view.SelectItem(ids[_clientIndex]);
            if (purchase)
            {
                if (view.Snapshot.Balance != 0 || !Field<Button>(view, "_action").interactable)
                { Fail("Zero-balance account cannot buy a free contract."); yield break; }
                Click(view, "_action");
                var box = FindObjectsOfType<MsgBox>().FirstOrDefault(b => b.Title == "购买物品");
                if (box == null || !box.Message.Contains("免费购买"))
                { Fail("Contract confirmation did not show its free price."); yield break; }
                Click(box, "_cancelButton");
                if (view.Snapshot.QuantityOf(ids[_clientIndex]) != 0 || GameController.Instance.State.Chips != 0)
                    Fail("Cancelling contract changed account.");
                Click(view, "_action");
                box = FindObjectsOfType<MsgBox>().First(b => b.Title == "购买物品");
                Click(box, "_confirmButton");
                if (Field<Button>(view, "_action").interactable) Fail("Contract pending button remained enabled.");
                yield return WaitForItems(panel);
                if (GameController.Instance.State.Chips != rewards[_clientIndex] || view.Snapshot.Balance != rewards[_clientIndex])
                    Fail("Contract reward differs from authoritative account balance.");
                foreach (var id in ids)
                {
                    view.SelectItem(id);
                    if (Field<Button>(view, "_action").interactable) Fail("Positive balance permits another contract purchase.");
                }
            }
            if (view.Snapshot.QuantityOf(ids[_clientIndex]) != 1) Fail("Contract ownership did not persist.");
            panel.Open(ShopPageMode.Collection);
            view.SelectItem(ids[_clientIndex]);
            var costs = new uint[] { 1100, 11000, 110000 };
            if (Field<Button>(view, "_action").interactable != (view.Snapshot.Balance >= costs[_clientIndex]))
                Fail("Contract repayment button differs from its authoritative balance condition.");
            Click(panel, "_back");
            Click(TitlePanel.Instance, "_start");
            WriteResult("contracts-" + _clientIndex + (purchase ? "-purchase" : "-reopen") + ".checked",
                "Production assets: free quote, zero-balance condition, cancel, reward, disabled ineligible buttons and consumable inventory.");
        }
    }
}
