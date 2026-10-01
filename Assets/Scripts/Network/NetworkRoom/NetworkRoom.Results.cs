using System;
using System.Linq;
using Framework.UI;
using Game.UI;
using Game.UI.GamePanelElement;
using Mirror;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        public RoundResult LastRoundResult { get; private set; }
        public RoundReadySync LastRoundReady { get; private set; }
        public event Action<RoundResult, bool> OnRoundResultChanged;
        public event Action<RoundReadySync> OnRoundReadyChanged;

        private void ClearClientResults()
        {
            LastRoundResult = null;
            LastRoundReady = default;
            OnRoundResultChanged?.Invoke(null, false);
            OnRoundReadyChanged?.Invoke(default);
        }

        private void RegisterResultServerHandlers()
        {
            RegisterServerHandler<RoundReadyRequest>(RoundReadyRequestHandler, true);
            RegisterServerHandler<RevealLastHandRequest>(RevealLastHandRequestHandler, true);
        }

        private void RegisterResultClientHandlers()
        {
            NetworkClient.RegisterHandler<RoundReadySync>(RoundReadySyncHandler, false);
            NetworkClient.RegisterHandler<RoundResultUpdate>(RoundResultUpdateHandler, false);
            NetworkClient.RegisterHandler<InsufficientChipsNotice>(InsufficientChipsNoticeHandler, false);
        }

        public bool RequestRoundReady(string handId)
        {
            if (!HasAuthenticatedClient || !_activeClientRoomId.HasValue ||
                LastRoundReady.HandId != handId || !LastRoundReady.RequiresConfirmation || LastRoundReady.IsConfirmed)
                return false;
            NetworkClient.Send(new RoundReadyRequest { RoomId = _activeClientRoomId.Value, HandId = handId });
            return true;
        }

        public bool RequestRevealLastHand(string handId)
        {
            if (!HasAuthenticatedClient || !_activeClientRoomId.HasValue ||
                LastRoundReady.HandId != handId || !LastRoundReady.CanReveal)
                return false;
            NetworkClient.Send(new RevealLastHandRequest { RoomId = _activeClientRoomId.Value, HandId = handId });
            return true;
        }

        public bool RequestWaitingParticipation(int roomId, bool participate)
        {
            if (!HasAuthenticatedClient || _activeClientRoomId != roomId) return false;
            NetworkClient.Send(new TableParticipationRequest
            {
                RoomId = roomId,
                HandId = string.Empty,
                RequireImmediate = true,
                Action = participate ? TableParticipationAction.JoinWaitingRoom : TableParticipationAction.ObserveWaitingRoom,
            });
            return true;
        }

        private void RoundReadyRequestHandler(NetworkConnectionToClient conn, RoundReadyRequest request)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var name) ||
                _roomManager.FindRoom(request.RoomId)?.ContainsMember(name) != true) return;
            _gameManager.ConfirmRound(request.RoomId, request.HandId, name, _playerManager.IsConnected);
            BroadcastRoundReady(request.RoomId);
        }

        private void RevealLastHandRequestHandler(NetworkConnectionToClient conn, RevealLastHandRequest request)
        {
            if (!TryGetAuthenticatedPlayer(conn, out var name) ||
                _roomManager.FindRoom(request.RoomId)?.ContainsMember(name) != true) return;
            _gameManager.RevealLastHand(request.RoomId, request.HandId, name, _playerManager.IsConnected);
            BroadcastRoundReady(request.RoomId);
        }

        private void BroadcastRoundReady(int roomId)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null) return;
            // GetRoundReady resolves seats and synchronizes the legacy player list.
            // Snapshot recipients before those lookups invalidate its enumerator.
            foreach (var name in room.MemberNames.ToArray())
                _playerManager.GetConnection(name)?.Send(_gameManager.GetRoundReady(roomId, name, _playerManager.IsConnected));
        }

        private void OnRoundResultRevealed(int roomId, RoundResult result)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null) return;
            var update = new RoundResultUpdate { RoomId = roomId, RoundResult = result };
            foreach (var name in room.MemberNames)
                _playerManager.GetConnection(name)?.Send(update);
        }

        private void RoundReadySyncHandler(RoundReadySync state)
        {
            if (!HasAuthenticatedClient || _activeClientRoomId != state.RoomId) return;
            LastRoundReady = state;
            OnRoundReadyChanged?.Invoke(state);
        }

        private void RoundResultUpdateHandler(RoundResultUpdate message)
        {
            if (!HasAuthenticatedClient || _activeClientRoomId != message.RoomId || LastRoundResult == null) return;
            var result = message.RoundResult;
            if (result == null || result.RoomId != message.RoomId || result.HandId != LastRoundResult.HandId) return;
            LastRoundResult = result;
            OnRoundResultChanged?.Invoke(result, false);
        }

        private void MoveInsufficientPlayersToSpectating(int roomId)
        {
            var room = _roomManager.FindRoom(roomId);
            if (room == null || _gameManager.IsHandInProgress(roomId)) return;
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository;
            var bigBlind = checked((uint)room.SmallBlindChips * 2U);
            foreach (var seat in room.Seats.ToArray())
            {
                var account = repository?.FindByUserName(seat.PlayerName);
                if (account == null) continue;
                _gameManager.RefreshWalletBalance(roomId, account);
                // Preserve queued departures until their room bookmark is durably cleared.
                // Moving the seat now would discard LeaveAfterHand before CompletePending.
                if (seat.LeaveAfterHand || account.Chips >= bigBlind) continue;
                _roomManager.MoveToSpectator(roomId, seat.PlayerName);
                _playerManager.GetConnection(seat.PlayerName)?.Send(new InsufficientChipsNotice
                {
                    RoomId = roomId, Chips = account.Chips, Dollars = account.Dollars, BigBlind = bigBlind,
                });
            }
        }

        private void InsufficientChipsNoticeHandler(InsufficientChipsNotice message)
        {
            if (!HasAuthenticatedClient || _activeClientRoomId != message.RoomId) return;
            var canExchange = (ulong)message.Chips + message.Dollars >= message.BigBlind;
            MsgBox.Create("InsufficientChips", box =>
            {
                box.Title = "筹码不足";
                box.Message = canExchange ? "您因不够一个大盲而转换为观战模式。是否前往兑换筹码？"
                    : "您因不够一个大盲而转换为观战模式";
                box.Type = canExchange ? MsgboxType.ConfirmAndCancel : MsgboxType.Confirm;
                box.ConfirmButtonText = canExchange ? "兑换筹码" : "知道了";
                box.CancelButtonText = "继续观战";
                if (canExchange)
                    box.OnConfirm += () =>
                    {
                        if (_activeClientRoomId == message.RoomId)
                            UIManager.Instance.PushPanel<ChipExchangePanel>();
                    };
            });
        }
    }
}
