using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.UI;
using Game.UI.Gifts;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    // Purchase, equip and consume requests for every item kind. Each response carries the account's inventory snapshot.
    public partial class NetworkRoom
    {
        private ItemRules _itemRules;
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _itemRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextItemRequest;

        public bool CanOpenShop => HasAuthenticatedClient && !_roomEntryPending;
        private IItemRepository ItemRepository => (authenticator as AccountAuthenticator)?.AccountRepository as IItemRepository;

        private void RegisterItemServerHandlers()
        {
            _itemRequests.Clear();
            _itemRules = new ItemRules(ServerConfiguration.Current.Catalog);
            WarnUnconfiguredInventory();
            RegisterServerHandler<ItemPurchaseRequest>((conn, request) =>
                SendItemResponse(conn, ProcessItemPurchase(conn, request)), requireAuthentication: true);
            RegisterServerHandler<ItemEquipRequest>((conn, request) =>
                SendItemResponse(conn, ProcessItemEquip(conn, request)), requireAuthentication: true);
            RegisterServerHandler<ItemConsumeRequest>((conn, request) =>
            {
                var response = ProcessItemConsume(conn, request, out var playback, out var room);
                SendItemResponse(conn, response);
                if (response.IsNewlyApplied && playback.Commands != null && room != null)
                    foreach (var memberName in room.MemberNames.Distinct(StringComparer.Ordinal))
                        _playerManager.GetConnection(memberName)?.Send(playback);
            }, requireAuthentication: true);
        }

        private void SendItemResponse(NetworkConnectionToClient conn, ItemActionResponse response)
        {
            if (response.HasSnapshot)
                conn.Send(new AccountBalanceSync { Chips = response.Snapshot.Chips, Dollars = response.Snapshot.Dollars });
            conn.Send(response);
            if (response.Result == ItemActionResult.Success && response.HasSnapshot)
                conn.Send(ReadCollection(conn));
        }

        // Shared request gate: authenticated session, increasing request IDs, bounded strings and a per-connection rate.
        private bool BeginItemRequest(NetworkConnectionToClient conn, ulong requestId, string operationId, string itemId,
            Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> requests, double interval,
            ref ItemActionResponse response, out PlayerSession session)
        {
            session = null;
            response.Result = ItemActionResult.Unavailable;
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || !_playerManager.TryGetSession(conn, out session))
                return false;
            response.AccountId = session.Account.AccountId;
            requests.TryGetValue(conn, out var previous);
            if (requestId == 0 || requestId <= previous.Sequence || (operationId?.Length ?? 0) > 32 ||
                (itemId?.Length ?? 0) > GameCatalog.MaxIdLength)
            {
                response.Result = ItemActionResult.InvalidRequest;
                return false;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            requests[conn] = (requestId, previous.Next);
            if (now < previous.Next)
            {
                response.Result = ItemActionResult.RateLimited;
                return false;
            }
            requests[conn] = (requestId, now + interval);
            if (ItemRepository == null || _itemRules == null) return false;
            UpdateWalletHandAccess(session.Account);
            return true;
        }

        internal ItemActionResponse ProcessItemPurchase(NetworkConnectionToClient conn, ItemPurchaseRequest request)
        {
            var response = new ItemActionResponse { RequestId = request.RequestId, OperationId = request.OperationId, Action = ItemAction.Purchase };
            if (!BeginItemRequest(conn, request.RequestId, request.OperationId, request.ItemId, _itemRequests, 0.2d, ref response, out var session))
                return response;
            if ((request.CatalogRevision?.Length ?? 0) > 64)
            {
                response.Result = ItemActionResult.InvalidRequest;
                return response;
            }
            var execution = ItemRepository.PurchaseItem(response.AccountId,
                request.OperationId, request.ItemId, request.Quantity, request.QuotedPrice, request.CatalogRevision, _itemRules,
                request.AuthorizeChipConversion, request.AuthorizedShortfall);
            response.Result = execution.Result;
            response.IsNewlyApplied = execution.IsNewlyApplied;
            if (execution.Outcome != null)
            {
                response.AppliedRewardDollars = execution.Outcome.RewardChips;
                response.HasFailedCondition = execution.Outcome.FailedCondition != null;
                if (response.HasFailedCondition) response.FailedCondition = ProjectCondition(execution.Outcome.FailedCondition);
            }
            if (response.Result == ItemActionResult.ConversionRequired)
                response.RequiredConversion = Shortfall(response.AccountId, (ulong)(_itemRules.Find(request.ItemId)?.Price ?? 0) * request.Quantity);
            return CompleteItemResponse(response, session, execution.IsNewlyApplied);
        }

        internal ItemActionResponse ProcessItemEquip(NetworkConnectionToClient conn, ItemEquipRequest request)
        {
            var response = new ItemActionResponse { RequestId = request.RequestId, OperationId = request.OperationId, Action = ItemAction.Equip };
            if (!BeginItemRequest(conn, request.RequestId, request.OperationId, request.ItemId, _itemRequests, 0.2d, ref response, out var session))
                return response;
            response.Result = InRoom(session) ? ItemActionResult.OutsideRoomRequired :
                ItemRepository.EquipItem(response.AccountId, request.OperationId, request.ItemId, _itemRules);
            return CompleteItemResponse(response, session, false);
        }

        internal ItemActionResponse ProcessItemConsume(NetworkConnectionToClient conn, ItemConsumeRequest request,
            out GiftPlaybackBatchMessage playback, out RoomInfo room)
        {
            playback = default;
            room = null;
            var response = new ItemActionResponse
            {
                RequestId = request.RequestId, OperationId = request.OperationId,
                Action = request.Method == ItemConsumeMethod.Give ? ItemAction.Give : ItemAction.Use,
            };
            // Gifts are sent in quick bursts, so giving has its own, shorter request interval.
            var requests = request.Method == ItemConsumeMethod.Give ? _giftRequests : _itemRequests;
            if (!BeginItemRequest(conn, request.RequestId, request.OperationId, null, requests,
                    request.Method == ItemConsumeMethod.Give ? 0.05d : 0.2d, ref response, out var session))
                return response;
            if (!Enum.IsDefined(typeof(ItemConsumeMethod), request.Method) || (request.CatalogRevision?.Length ?? 0) > 64 ||
                request.Commands == null || request.Commands.Length == 0 || request.Commands.Length > GiftBatchLimits.MaxCommands ||
                request.Commands.Any(c => (c.ItemId?.Length ?? 0) > GameCatalog.MaxIdLength))
            {
                response.Result = ItemActionResult.InvalidRequest;
                return response;
            }
            var commands = request.Commands.Select(c => new ItemConsumeCommand(c.ItemId, c.Quantity,
                request.Method == ItemConsumeMethod.Give ? c.TargetSeatIndex : -1)).ToArray();
            var location = CurrentLocation(session, out room, out var senderSeat);
            if (request.Method == ItemConsumeMethod.Give)
            {
                var check = ValidateGive(commands, room, senderSeat);
                if (check != ItemActionResult.Success)
                {
                    response.Result = check;
                    return CompleteItemResponse(response, session, false);
                }
            }
            var execution = ItemRepository.ConsumeItems(response.AccountId, request.OperationId, request.Method, commands,
                request.CatalogRevision, _itemRules, location, request.AuthorizeChipConversion, request.AuthorizedShortfall);
            response.Result = execution.Result;
            response.IsNewlyApplied = execution.IsNewlyApplied;
            if (response.Result == ItemActionResult.ConversionRequired && request.Method == ItemConsumeMethod.Use)
                response.RequiredConversion = Shortfall(response.AccountId, _itemRules.Find(commands[0].ItemId)?.UsePolicy?.Cost ?? 0);
            if (request.Method == ItemConsumeMethod.Give && execution.Result == ItemActionResult.Success && execution.IsNewlyApplied)
                playback = new GiftPlaybackBatchMessage
                {
                    EventId = request.OperationId,
                    StartAtNetworkTime = NetworkTime.time + 0.1d,
                    Commands = execution.Applied.Select(command => new GiftPlaybackCommandMessage
                    {
                        ItemId = command.ItemId, SenderSeatIndex = senderSeat.SeatIndex,
                        TargetSeatIndex = command.TargetSeatIndex, Quantity = command.Quantity,
                    }).ToArray(),
                };
            return CompleteItemResponse(response, session, execution.IsNewlyApplied);
        }

        private uint Shortfall(string accountId, ulong cost)
        {
            var current = WalletRepository?.ReadWallet(accountId)?.Account;
            return current != null && cost > current.Dollars && cost - current.Dollars <= uint.MaxValue
                ? (uint)(cost - current.Dollars) : 0;
        }

        private ItemActionResponse CompleteItemResponse(ItemActionResponse response, PlayerSession session, bool changedWallet)
        {
            if (changedWallet)
            {
                var updated = WalletRepository?.ReadWallet(response.AccountId)?.Account;
                if (updated != null) SynchronizeWalletBalance(updated);
            }
            response.HasSnapshot = TryReadInventory(session, out response.Snapshot);
            return response;
        }

        private bool InRoom(PlayerSession session) =>
            _roomManager.WhichRoom(session.Account.UserName, out _) || session.Account.LastRoomId.HasValue;

        private ItemUseLocation CurrentLocation(PlayerSession session, out RoomInfo room, out RoomSeatInfo seat)
        {
            room = null;
            seat = null;
            if (_roomManager.WhichRoom(session.Account.UserName, out var roomId))
            {
                room = _roomManager.FindRoom(roomId);
                seat = room?.FindSeat(session.Account.UserName);
                return room != null && !room.IsWaiting ? ItemUseLocation.StartedRoom : ItemUseLocation.WaitingRoom;
            }
            return session.Account.LastRoomId.HasValue ? ItemUseLocation.WaitingRoom : ItemUseLocation.OutsideRoom;
        }

        /// <summary>Reads the authoritative inventory and the per-account verdict on every offer and owned item.</summary>
        internal bool TryReadInventory(PlayerSession session, out InventorySnapshotMessage snapshot)
        {
            snapshot = default;
            var inventory = ItemRepository?.ReadItems(session.Account.AccountId);
            if (inventory == null || _itemRules == null) return false;
            var account = inventory.Account;
            session.Account = account.Copy();
            var wallet = ReadCurrentWallet(account);
            var availableChips = wallet?.AvailableChips ?? 0;
            var canExchange = wallet?.CanExchange ?? false;
            var location = CurrentLocation(session, out _, out _);
            var owned = inventory.Items.ToDictionary(stack => stack.ItemId, stack => stack.Quantity, StringComparer.Ordinal);
            // Eligibility counts chips that could be exchanged, so the UI can offer a conversion instead of refusing.
            uint Power(ulong cost) => canExchange && cost > account.Dollars && cost - account.Dollars <= availableChips
                ? (uint)Math.Min(uint.MaxValue, cost) : account.Dollars;
            snapshot = new InventorySnapshotMessage
            {
                Dollars = account.Dollars,
                Chips = account.Chips,
                AvailableChips = availableChips,
                CanExchange = canExchange,
                Items = inventory.Items.Select(stack => new ItemStackMessage { ItemId = stack.ItemId, Quantity = stack.Quantity }).ToArray(),
                EquippedNamecard = account.EquippedNamecardId ?? string.Empty,
                EquippedCardFace = account.EquippedCardFaceId ?? string.Empty,
                CanAct = true,
                CanEquip = location == ItemUseLocation.OutsideRoom,
                CatalogRevision = _itemRules.Revision,
                Offers = _itemRules.Offers.Where(rule => rule.Listed).Select(rule =>
                {
                    owned.TryGetValue(rule.Id, out var quantity);
                    var reason = ItemRules.CheckPurchase(new PurchaseContext(account.AccountId, Power(rule.Price), rule, quantity,
                        account.TotalAssets), 1, out _);
                    return new ItemOfferStateMessage { ItemId = rule.Id, CanPurchase = reason == ItemActionResult.Success, BlockReason = reason };
                }).ToArray(),
                Usable = inventory.Items.Select(stack => _itemRules.Find(stack.ItemId)).Where(rule => rule?.UsePolicy != null &&
                    ItemRules.CheckUse(new ItemUseContext(account.AccountId, Power(rule.UsePolicy.Cost), rule, owned[rule.Id]), location) ==
                    ItemActionResult.Success).Select(rule => rule.Id).ToArray(),
            };
            return true;
        }

        private static PurchaseConditionMessage ProjectCondition(PurchaseConditionDescriptor condition)
            => new PurchaseConditionMessage { Kind = condition.Kind, Comparison = condition.Comparison, Threshold = condition.Threshold, Version = condition.Version };

        // Items can only be delisted, never removed while players own them; report owned IDs that are gone.
        private void WarnUnconfiguredInventory()
        {
            if (!(authenticator is AccountAuthenticator accounts) || !(accounts.AccountRepository is IStoredItemRepository repository))
                return;
            var missing = repository.ReadStoredItemIds().Where(id => _itemRules.Find(id) == null).ToArray();
            if (missing.Length > 0)
                Framework.Log.Log.WriteWarn("Players own items missing from Configs/Items/Items.json; restore them: " + string.Join(", ", missing));
        }

        public ulong RequestItemPurchase(ItemPurchaseRequest request)
        {
            if (!CanOpenShop) return 0;
            InvalidateCollection();
            request.RequestId = ++_nextItemRequest;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        public ulong RequestItemEquip(ItemEquipRequest request)
        {
            if (!CanOpenShop) return 0;
            InvalidateCollection();
            request.RequestId = ++_nextItemRequest;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        public ulong RequestItemConsume(ItemConsumeRequest request)
        {
            if (!HasAuthenticatedClient || request.Commands == null || request.Commands.Length == 0) return 0;
            InvalidateCollection();
            request.RequestId = ++_nextItemRequest;
            NetworkClient.Send(request);
            return request.RequestId;
        }

        private void RegisterItemClientHandlers()
        {
            NetworkClient.RegisterHandler<ItemActionResponse>(response =>
            {
                if (!HasAuthenticatedClient || response.AccountId != GameController.Instance.State.AccountId) return;
                if (response.HasSnapshot)
                {
                    CardFaceAppearance.Equip(response.Snapshot.EquippedCardFace);
                    EquippedArtwork.PreloadNamecard(response.Snapshot);
                }
                if (response.Action == ItemAction.Give) GiftInteractionController.EnsureActive()?.ReceiveGiveResponse(response);
                else if (CanOpenShop) ShopPanel.Instance?.ReceiveItemAction(response);
            });
        }
    }
}
