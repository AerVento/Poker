using System.Collections;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mirror;
using UnityEngine;
using UnityEngine.UI;
using Game.UI;
using Game.UI.GamePanelElement;

namespace Game.Network.Diagnostics
{
    public sealed partial class PokerIntegrationRunner
    {
        private string _reviewScenario;
        private bool _testResultReview;
        private float _lastResultTime;
        private bool _reviewIntentSent;
        private bool _reviewChecked;
        private bool _reviewActionSent;

        private void InspectResultReview(GameInfoSync message)
        {
            var info = message.Info;
            if (info.IsSpectator && info.Hole.Count != 0)
                Fail("Observer received private cards.");
            if (_reviewScenario != "review-actions" || _clientIndex == 2)
                return;
            if (info.Round < GameStage.RoundOver && !_reviewIntentSent)
            {
                _reviewIntentSent = true;
                Click(GamePanel.Instance, _clientIndex == 0 ? "_leaveButton" : "_participationButton");
                if (_clientIndex == 0)
                {
                    var confirm = FindObjectsOfType<MsgBox>().FirstOrDefault(box => box.Title == "本手结束后离开");
                    if (confirm == null) Fail("Departure confirmation did not open.");
                    else Click(confirm, "_confirmButton");
                }
            }
            if (message.LeaveAfterHand || message.SitOutAfterHand)
                WriteResult("review-intent-" + _clientIndex, "queued");
            if (info.Round == GameStage.RoundOver && !_reviewActionSent)
            {
                if (info.IsSpectator || (_clientIndex == 0 ? !message.LeaveAfterHand : !message.SitOutAfterHand))
                    Fail("Queued membership changed before result viewing finished.");
                if (Field<TMPro.TMP_Text>(GamePanel.Instance, "_leaveButtonLabel").text != "离开房间" ||
                    Field<TMPro.TMP_Text>(GamePanel.Instance, "_participationButtonLabel").text != "转为旁观")
                    Fail("Review UI did not expose immediate actions.");
            }
        }

        private void TryReviewOperate(GameInfoSync message)
        {
            var info = message.Info;
            if (_finishing || _actionPending || info.Round >= GameStage.RoundOver || info.IsSpectator ||
                info.CurrentPlayerIndex != info.OperatingPlayer ||
                !File.Exists(Path.Combine(_outputDirectory, "client-2.joined")))
                return;
            if (_reviewScenario == "review-actions" && (!File.Exists(Path.Combine(_outputDirectory, "review-intent-0")) ||
                !File.Exists(Path.Combine(_outputDirectory, "review-intent-1"))))
                return;

            var operation = _reviewScenario == "review-bust"
                ? info.CurrentPlayer.StageBet >= message.BigBlind ? PlayerOperationType.Fold : PlayerOperationType.AllIn
                : info.Round == GameStage.Flop && info.CurrentHighestPlayerBet == 0
                    ? PlayerOperationType.Bet
                    : info.CurrentPlayer.StageBet < info.CurrentHighestPlayerBet ? PlayerOperationType.Call : PlayerOperationType.Check;
            _actionPending = true;
            var area = Field<OperationArea>(GamePanel.Instance, "_operationArea");
            if (operation == PlayerOperationType.Bet)
                Field<TMPro.TMP_InputField>(area, "_betInput").text = info.LeastRaise.ToString();
            var button = operation == PlayerOperationType.Bet ? "_betOrRaiseButton" :
                operation == PlayerOperationType.Check ? "_checkButton" :
                operation == PlayerOperationType.Call ? "_callButton" :
                operation == PlayerOperationType.AllIn ? "_allInButton" : "_foldButton";
            Click(area, button);
            var confirmation = Field<MsgBox>(area, "_confirmation");
            if (confirmation != null) Click(confirmation, "_confirmButton");
        }

        private IEnumerator CheckResultViewing()
        {
            // Check after multiple rendered frames, not just at receipt of a result message.
            yield return new WaitForSecondsRealtime(1.5f);
            if (_finishing) yield break;
            var result = RoundResultMsgBox.Instance;
            if (result == null || !result.IsShowing || !GamePanel.Instance.IsShowing ||
                !_latestGameInfo.HasValue || _latestGameInfo.Value.Info.Round != GameStage.RoundOver ||
                _latestGameInfo.Value.Info.RoomInfo.IsWaiting)
            {
                Fail("Result panel was not retained during the review interval.");
                yield break;
            }
            if (_reviewScenario == "review-bust" &&
                !_latestGameInfo.Value.Info.Players.Any(player => player.IsOccupied && player.Chips < _latestGameInfo.Value.BigBlind))
            {
                Fail("The two-player scenario did not produce a balance below the big blind.");
                yield break;
            }
            _reviewChecked = true;
            if (_reviewScenario == "review-actions" && _clientIndex < 2)
            {
                _reviewActionSent = true;
                Click(result, "_confirmButton");
                Click(GamePanel.Instance, _clientIndex == 0 ? "_leaveButton" : "_participationButton");
                yield return new WaitForSecondsRealtime(0.5f);
                if (_clientIndex == 1 && (!_latestGameInfo.HasValue || !_latestGameInfo.Value.Info.IsSpectator ||
                    _latestGameInfo.Value.Info.RoomInfo.FindSeat(_userName) != null))
                    Fail("Immediate spectating did not release the seat during review.");
            }
            else
            {
                yield return new WaitForSecondsRealtime(6f);
                if (!_finishing && (RoundResultMsgBox.Instance == null || !RoundResultMsgBox.Instance.IsShowing ||
                    !GamePanel.Instance.IsShowing || _latestGameInfo.Value.Info.RoomInfo.IsWaiting))
                    Fail("Another member's departure or insufficient chips interrupted result viewing.");
            }
        }

        private IEnumerator FinishResultReviewClient()
        {
            _finishing = true;
            if (!_reviewChecked || _roundsCompleted != 1 || !_sawCountdown)
            {
                Fail("Result-review checkpoints were not all reached.");
                yield break;
            }
            var elapsed = Time.realtimeSinceStartup - _lastResultTime;
            if (_reviewScenario == "review-actions" && _clientIndex == 0 && elapsed >= 5)
            {
                Fail("Explicit departure was deferred instead of immediate.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.2f);
            LoginCredentialStore.Clear();
            _networkRoom.StopClient();
            yield return null;
            if (!TitlePanel.Instance.IsShowing)
            {
                Fail("Disconnect did not return to the title UI.");
                yield break;
            }
            WriteResult(ResultPath(_clientIndex, "pass"), JsonSerializer.Serialize(new
            {
                Client = _clientIndex, Scenario = _reviewScenario, ReviewSeconds = elapsed,
                ResultVisible = _reviewChecked, ImmediateAction = _reviewActionSent,
                ReturnedToWaitingRoom = _sawWaitingRoom, ShippingUIButtons = true, DisconnectedToTitle = true,
            }));
            Application.Quit(0);
        }
    }
}
