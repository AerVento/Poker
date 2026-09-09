using System;
using System.Collections;
using System.IO;
using System.Linq;
using Framework.UI;
using Game.Network.Accounts;
using Game.UI;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private bool _testInGameTasks;
        private bool _inGameTaskViewStarted;
        private bool _inGameTaskFoldStarted;

        private bool CheckInGameTaskPhase(GameInfoSync message)
        {
            if (_clientIndex == 0 && !_inGameTaskViewStarted && _roundsCompleted == 0)
            {
                _inGameTaskViewStarted = true;
                StartCoroutine(CheckInGameTaskUI("view"));
            }
            if (!File.Exists(Path.Combine(_outputDirectory, "tasks-view.checked"))) return true;
            if (_roundsCompleted != 1 || message.Info.Round >= GameStage.RoundOver) return false;
            if (!File.Exists(Path.Combine(_outputDirectory, "tasks-between.checked"))) return true;
            if (message.Info.Players.Any(p => p.IsFold) &&
                !File.Exists(Path.Combine(_outputDirectory, "tasks-folded.checked")))
            {
                if (_clientIndex == 0 && !_inGameTaskFoldStarted && message.CanClaimDailyTaskRewards)
                {
                    _inGameTaskFoldStarted = true;
                    StartCoroutine(CheckInGameTaskUI("folded"));
                }
                return true;
            }
            return false;
        }

        private IEnumerator CheckInGameTaskUI(string phase)
        {
            yield return null;
            var readonlyPhase = phase == "view";
            var before = GameController.Instance.State.Chips;
            var hand = _latestGameInfo.Value.Info;
            var stack = hand.CurrentPlayer.Chips;
            Click(GamePanel.Instance, "_settingsButton");
            Click(InGameOptionPanel.Instance, "_tasks");
            yield return WaitForDailyTaskBalance(before);
            if (_finishing) yield break;
            var panel = DailyTaskPanel.Instance;
            var snapshot = Field<DailyTasksSnapshot>(panel, "_snapshot");
            var state = snapshot.Tasks.First(t => t.Id == DailyTaskId.DailySupply);
            var card = Field<DailyTaskCard[]>(panel, "_cards").First(c => c.TaskId == DailyTaskId.DailySupply);
            if (_networkRoom.CanClaimDailyTasks == readonlyPhase || Field<Button>(card, "_claim").interactable == readonlyPhase)
            { Fail("Task claim eligibility differs from live game phase: " + phase); yield break; }
            if (readonlyPhase)
            {
                if (Field<DailyTaskCard[]>(panel, "_cards").Any(c => Field<Button>(c, "_claim").interactable || Field<Button>(c, "_delete").interactable))
                { Fail("Active-player task panel contains enabled mutation buttons."); yield break; }
                var sequence = Field<ulong>(_networkRoom, "_nextDailyTaskRequest") + 1;
                typeof(NetworkRoom).GetField("_nextDailyTaskRequest", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(_networkRoom, sequence);
                NetworkClient.Send(new DailyTaskClaimRequest { RequestId = sequence, InstanceId = state.InstanceId, CompletionOrdinal = state.ClaimOrdinal });
                yield return WaitForDailyTaskClaim(sequence, DailyTaskResult.ActiveHand);
            }
            else
            {
                yield return ClaimDailyTaskThroughUI(DailyTaskId.DailySupply, before + 1000);
                if (_finishing) yield break;
                if (phase == "folded")
                {
                    var current = _latestGameInfo.Value.Info;
                    if (current.HandId != hand.HandId || current.Round >= GameStage.RoundOver ||
                        !current.CurrentPlayer.IsFold || current.CurrentPlayer.Chips != stack || current.TotalPot != hand.TotalPot)
                    { Fail("Folded reward changed the current hand or its stack/pot."); yield break; }
                }
                var duplicate = _networkRoom.RequestDailyTaskClaim(state.InstanceId, state.ClaimOrdinal);
                yield return WaitForDailyTaskClaim(duplicate, DailyTaskResult.AlreadyClaimed);
            }
            if (_finishing) yield break;
            Click(panel, "_back");
            UIManager.Instance.PopPanel<InGameOptionPanel>();
            WriteResult("tasks-" + phase + ".checked", "Shipping settings/task UI, authoritative phase validation, reward replay and stack isolation verified.");
        }
    }
}
