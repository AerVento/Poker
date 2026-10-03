using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private SlotLotteryCatalog _slotCatalog;
        private readonly Dictionary<NetworkConnectionToClient, (ulong Id, double Next)> _slotRequests = new Dictionary<NetworkConnectionToClient, (ulong, double)>();
        private ulong _nextSlotRequest;
        public event Action<SlotLotteryResponse> SlotLotteryReceived;
        public bool CanOpenSlotLottery => HasAuthenticatedClient && !_roomEntryPending;
        private ISlotLotteryRepository SlotRepository => (authenticator as AccountAuthenticator)?.AccountRepository as ISlotLotteryRepository;

        private void RegisterSlotLotteryServerHandlers()
        {
            _slotRequests.Clear();
            try { _slotCatalog = ServerConfiguration.Current.SlotLottery; }
            catch (Exception exception) { HandleFatalStorageError(exception); return; }
            RegisterServerHandler<SlotLotteryRequest>((conn, request) =>
            {
                var response = ProcessSlotLotteryRequest(conn, request);
                if (!string.IsNullOrEmpty(response.AccountId))
                    conn.Send(new AccountBalanceSync { Dollars = response.Dollars, Chips = response.Chips });
                conn.Send(response);
            }, requireAuthentication: true);
        }

        internal SlotLotteryResponse ProcessSlotLotteryRequest(NetworkConnectionToClient conn, SlotLotteryRequest request)
        {
            var response = new SlotLotteryResponse { RequestId = request.RequestId, Action = request.Action, Result = SlotLotteryResult.Unavailable };
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || !_playerManager.TryGetSession(conn, out var session) || SlotRepository == null) return response;
            response.AccountId = session.Account.AccountId;
            response.CostDollars = _slotCatalog?.CostDollars ?? 0;
            response.Revision = _slotCatalog?.Revision;
            response.Enabled = _slotCatalog?.Enabled ?? false;
            _slotRequests.TryGetValue(conn, out var prior);
            bool valid = request.RequestId > prior.Id && request.RequestId != 0 &&
                Enum.IsDefined(typeof(SlotLotteryAction), request.Action) &&
                (request.Action != SlotLotteryAction.Draw || request.Count == 1 || request.Count == 10) && (request.Revision?.Length ?? 0) <= 64 &&
                (request.OperationId?.Length ?? 0) <= 32 &&
                (request.Action == SlotLotteryAction.Status || request.Action == SlotLotteryAction.Pool || Guid.TryParseExact(request.OperationId, "N", out _)) &&
                (request.Action != SlotLotteryAction.Pool || request.Tier >= SlotPrizeTier.Small && request.Tier <= SlotPrizeTier.Grand && request.Offset >= 0 && request.Offset < 200);
            double now = Time.realtimeSinceStartupAsDouble;
            if (!valid) response.Result = SlotLotteryResult.InvalidRequest;
            else if (now < prior.Next) response.Result = SlotLotteryResult.RateLimited;
            else
            {
                _slotRequests[conn] = (request.RequestId, now + 0.15);
                UpdateWalletHandAccess(session.Account);
                if (request.Action == SlotLotteryAction.Draw)
                {
                    var draw = SlotRepository.DrawSlot(response.AccountId, request.OperationId, request.Revision, _slotCatalog, ItemCatalog, request.Count);
                    response.Result = draw.Result;
                    response.Receipt = draw.Receipt;
                    if (draw.NewlyApplied) conn.Send(ReadCollection(conn));
                }
                else if (request.Action == SlotLotteryAction.Pool) response.Result = SlotLotteryResult.Success;
                else
                {
                    if (request.Action == SlotLotteryAction.Acknowledge) SlotRepository.AcknowledgeSlot(response.AccountId, request.OperationId);
                    response.Result = SlotLotteryResult.Success;
                    response.Receipt = SlotRepository.ReadPendingSlot(response.AccountId);
                }
            }
            // Disclosed after the draw so the odds already exclude anything this response just granted.
            bool pool = request.Action == SlotLotteryAction.Pool && response.Result == SlotLotteryResult.Success;
            response.Disclosure = ReadSlotDisclosure(response.AccountId, pool ? request.Tier : SlotPrizeTier.None, pool ? request.Offset : 0);
            if (pool && response.Disclosure == null) response.Result = SlotLotteryResult.Unavailable;
            var wallet = WalletRepository.ReadWallet(response.AccountId);
            if (wallet != null)
            {
                session.Account = wallet.Account.Copy();
                response.Dollars = wallet.Account.Dollars;
                response.Chips = wallet.Account.Chips;
                response.AvailableChips = wallet.AvailableChips;
                response.CanExchange = wallet.CanExchange;
                if (request.Action == SlotLotteryAction.Draw && response.Result == SlotLotteryResult.Success)
                    SynchronizeWalletBalance(wallet.Account);
            }
            return response;
        }

        /// <summary>Per-player odds: rewards that would overflow a holding limit are excluded, exactly as in the draw.</summary>
        private SlotLotteryDisclosure ReadSlotDisclosure(string accountId, SlotPrizeTier tier, int offset)
        {
            var inventory = ItemRepository?.ReadItems(accountId);
            var items = ItemCatalog;
            if (_slotCatalog == null || inventory == null || items == null) return null;
            var owned = inventory.Items.ToDictionary(stack => stack.ItemId, stack => stack.Quantity, StringComparer.Ordinal);
            var limits = items.Items.ToDictionary(item => item.Id, item => item.MaxQuantity, StringComparer.Ordinal);
            var disclosure = SlotLotteryDisclosure.From(_slotCatalog, tier, offset, reward => SlotLotteryCatalog.Fits(reward, owned, limits));
            disclosure.PityMisses = SlotRepository?.ReadSlotMisses(accountId) ?? 0;
            return disclosure;
        }

        public ulong RequestSlotLottery(SlotLotteryAction action, string operationId = null, string revision = null, SlotPrizeTier tier = SlotPrizeTier.None, int offset = 0, byte count = 1)
        {
            if (!CanOpenSlotLottery) return 0;
            var id = ++_nextSlotRequest;
            NetworkClient.Send(new SlotLotteryRequest { RequestId = id, Action = action, OperationId = operationId, Revision = revision, Tier = tier, Offset = offset, Count = count });
            return id;
        }

        private void RegisterSlotLotteryClientHandlers()
        {
            NetworkClient.RegisterHandler<SlotLotteryResponse>(response =>
            {
                if (HasAuthenticatedClient && response.AccountId == GameController.Instance.State.AccountId)
                    SlotLotteryReceived?.Invoke(response);
            });
        }
    }
}
