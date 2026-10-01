using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network.Accounts;
using Game.UI.Gifts;
using Mirror;
using UnityEngine;

namespace Game.Network
{
    public partial class NetworkRoom
    {
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _giftUseRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private readonly Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> _giftInventoryRequests =
            new Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)>();
        private ulong _nextGiftRequest;

        private void RegisterGiftServerHandlers()
        {
            _giftUseRequests.Clear();
            _giftInventoryRequests.Clear();
            RegisterServerHandler<GiftInventoryRequest>((conn, request) =>
                conn.Send(ProcessGiftInventoryRequest(conn, request)), requireAuthentication: true);
            RegisterServerHandler<GiftUseBatchRequest>((conn, request) =>
            {
                var response = ProcessGiftUseBatchRequest(conn, request, out var playback, out var room);
                conn.Send(response);
                if (response.HasSnapshot && _playerManager.TryGetSession(conn, out var updatedSession))
                {
                    conn.Send(new AccountBalanceSync { Chips = updatedSession.Account.Chips, Dollars = response.Dollars });
                    SynchronizeWalletBalance(updatedSession.Account);
                }
                if (response.Result == GiftUseResult.Success)
                    conn.Send(ReadCollection(conn));
                if (!response.IsNewlyApplied || playback.Commands == null || room == null)
                    return;
                foreach (var memberName in room.MemberNames.Distinct(StringComparer.Ordinal))
                    _playerManager.GetConnection(memberName)?.Send(playback);
            }, requireAuthentication: true);
        }

        private GiftInventoryResponse ProcessGiftInventoryRequest(NetworkConnectionToClient conn, GiftInventoryRequest request)
        {
            var response = new GiftInventoryResponse
            {
                RequestId = request.RequestId,
                Result = GiftUseResult.Unavailable,
                Items = Array.Empty<ItemStackMessage>(),
            };
            if (!TryValidateGiftSession(conn, request.RequestId, _giftInventoryRequests,
                out var session, out var room, out var senderSeat))
            {
                response.Result = GiftUseResult.InvalidRequest;
                return response;
            }
            var now = Time.realtimeSinceStartupAsDouble;
            var previous = _giftInventoryRequests[conn];
            if (now < previous.Next)
            {
                response.Result = GiftUseResult.RateLimited;
                return response;
            }
            _giftInventoryRequests[conn] = (request.RequestId, now + 0.2d);
            if (room == null || room.IsWaiting)
            {
                response.Result = GiftUseResult.RoomNotStarted;
                return response;
            }
            if (senderSeat == null || senderSeat.State == RoomSeatState.Leaving)
            {
                response.Result = GiftUseResult.SenderNotSeated;
                return response;
            }
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IItemRepository;
            var snapshot = repository?.ReadItems(session.Account.AccountId);
            if (snapshot == null || _itemRuntime == null)
                return response;
            response.Result = GiftUseResult.Success;
            response.Items = ProjectGiftItems(snapshot.Items);
            response.Dollars = snapshot.Account.Dollars;
            response.CanSend = CanSendGifts(snapshot.Items);
            var wallet = ReadCurrentWallet(snapshot.Account);
            response.AvailableChips = wallet?.AvailableChips ?? 0;
            response.CanExchange = wallet?.CanExchange ?? false;
            response.Offers = ProjectGiftOffers(snapshot.Account, wallet);
            return response;
        }

        internal GiftUseBatchResponse ProcessGiftUseBatchRequest(NetworkConnectionToClient conn,
            GiftUseBatchRequest request, out GiftPlaybackBatchMessage playback, out RoomInfo room)
        {
            playback = default;
            room = null;
            var response = new GiftUseBatchResponse
            {
                RequestId = request.RequestId,
                OperationId = request.OperationId,
                Result = GiftUseResult.Unavailable,
                Items = Array.Empty<ItemStackMessage>(),
            };
            if (!TryValidateGiftSession(conn, request.RequestId, _giftUseRequests,
                out var session, out room, out var senderSeat))
            {
                response.Result = GiftUseResult.InvalidRequest;
                return response;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            var previous = _giftUseRequests[conn];
            if (now < previous.Next)
            {
                response.Result = GiftUseResult.RateLimited;
                return response;
            }
            _giftUseRequests[conn] = (request.RequestId, now + 0.05d);

            if (room == null || room.IsWaiting)
            {
                response.Result = GiftUseResult.RoomNotStarted;
                return PopulateGiftItems(response, session);
            }
            if (senderSeat == null || senderSeat.State == RoomSeatState.Leaving)
            {
                response.Result = GiftUseResult.SenderNotSeated;
                return PopulateGiftItems(response, session);
            }
            if (!TryValidateGiftCommands(request.Commands, room, senderSeat.SeatIndex,
                out var commands, out var validationResult))
            {
                response.Result = validationResult;
                return PopulateGiftItems(response, session);
            }

            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IGiftRepository;
            if (repository == null || _itemRuntime == null)
                return PopulateGiftItems(response, session);
            UpdateWalletHandAccess(session.Account);
            var execution = repository.ConsumeGiftBatch(session.Account.AccountId,
                request.OperationId, commands, _itemRuntime);
            response.Result = execution.Result;
            response.IsNewlyApplied = execution.IsNewlyApplied;
            response = PopulateGiftItems(response, session);
            if (execution.Result == GiftUseResult.Success && execution.IsNewlyApplied)
            {
                playback = new GiftPlaybackBatchMessage
                {
                    EventId = request.OperationId,
                    StartAtNetworkTime = NetworkTime.time + 0.1d,
                    Commands = execution.AppliedCommands.Select(command => new GiftPlaybackCommandMessage
                    {
                        ItemId = command.ItemId,
                        SenderSeatIndex = senderSeat.SeatIndex,
                        TargetSeatIndex = command.TargetSeatIndex,
                        Quantity = command.Quantity,
                    }).ToArray(),
                };
            }
            return response;
        }

        private bool TryValidateGiftSession(NetworkConnectionToClient conn, ulong requestId,
            Dictionary<NetworkConnectionToClient, (ulong Sequence, double Next)> requests,
            out PlayerSession session, out RoomInfo room, out RoomSeatInfo senderSeat)
        {
            session = null;
            room = null;
            senderSeat = null;
            if (_serverStorageFailed || conn == null || !conn.isAuthenticated || requestId == 0 ||
                !_playerManager.TryGetSession(conn, out session))
                return false;
            requests.TryGetValue(conn, out var previous);
            if (requestId <= previous.Sequence)
                return false;
            requests[conn] = (requestId, previous.Next);
            if (!_roomManager.WhichRoom(session.Account.UserName, out var roomId))
                return true;
            room = _roomManager.FindRoom(roomId);
            senderSeat = room?.FindSeat(session.Account.UserName);
            return true;
        }

        private static bool TryValidateGiftCommands(GiftUseCommandMessage[] messages, RoomInfo room,
            int senderSeatIndex, out GiftUseCommand[] commands, out GiftUseResult result)
        {
            commands = null;
            result = GiftUseResult.InvalidRequest;
            if (messages == null || messages.Length == 0 || messages.Length > GiftBatchLimits.MaxCommands)
                return false;
            ulong total = 0;
            var projected = new GiftUseCommand[messages.Length];
            for (var index = 0; index < messages.Length; index++)
            {
                var message = messages[index];
                if (!NamecardServerCatalog.IsValidId(message.ItemId) || message.Quantity == 0)
                    return false;
                total += message.Quantity;
                if (total > GiftBatchLimits.MaxEffects)
                    return false;
                var target = room.FindSeat(message.TargetSeatIndex);
                if (target == null || target.State == RoomSeatState.Leaving)
                {
                    result = GiftUseResult.TargetNotFound;
                    return false;
                }
                if (message.TargetSeatIndex == senderSeatIndex)
                {
                    result = GiftUseResult.CannotTargetSelf;
                    return false;
                }
                projected[index] = new GiftUseCommand(message.ItemId, message.TargetSeatIndex, message.Quantity);
            }
            commands = projected;
            result = GiftUseResult.Success;
            return true;
        }

        private GiftUseBatchResponse PopulateGiftItems(GiftUseBatchResponse response, PlayerSession session)
        {
            var repository = (authenticator as AccountAuthenticator)?.AccountRepository as IItemRepository;
            var snapshot = repository?.ReadItems(session.Account.AccountId);
            if (snapshot != null)
            {
                response.HasSnapshot = true;
                response.Items = ProjectGiftItems(snapshot.Items);
                response.Dollars = snapshot.Account.Dollars;
                response.CanSend = CanSendGifts(snapshot.Items);
                var wallet = ReadCurrentWallet(snapshot.Account);
                response.AvailableChips = wallet?.AvailableChips ?? 0;
                response.CanExchange = wallet?.CanExchange ?? false;
                response.Offers = ProjectGiftOffers(snapshot.Account, wallet);
                session.Account = snapshot.Account.Copy();
            }
            return response;
        }

        private static bool CanSendGifts(IEnumerable<ItemStack> items)
        {
            var stacks = items.ToDictionary(item => item.ItemId, item => item.Quantity);
            stacks.TryGetValue("devil-contract", out var devil);
            stacks.TryGetValue("archdevil-contract", out var archdevil);
            stacks.TryGetValue("satan-contract", out var satan);
            return !InfernalNamecard.IsForced(InfernalNamecard.CalculateYears(devil, archdevil, satan));
        }

        private GiftOfferMessage[] ProjectGiftOffers(PlayerAccount account, WalletSnapshot wallet)
        {
            return _itemRuntime.Products.Where(product => product.UsePolicy?.Kind == ItemUseKind.Gift)
                .Select(product => new GiftOfferMessage
                {
                    ItemId = product.Id,
                    Price = product.Price,
                    CanPurchase = product.Policy.OnPurchaseEffects.Count == 0 &&
                        ItemPurchaseRuntime.CheckPurchase(new PurchaseContext(account.AccountId,
                            (uint)Math.Min(uint.MaxValue, CurrencySystem.PurchasingPower(account.Dollars,
                                wallet?.AvailableChips ?? 0, wallet?.CanExchange ?? false)), product, 0, account.TotalAssets), false, out _) == ItemShopResult.Success,
                }).ToArray();
        }

        private ItemStackMessage[] ProjectGiftItems(IEnumerable<ItemStack> items)
        {
            if (_itemRuntime == null)
                return Array.Empty<ItemStackMessage>();
            return (items ?? Array.Empty<ItemStack>())
                .Where(stack => _itemRuntime.Find(stack.ItemId)?.UsePolicy?.Kind == ItemUseKind.Gift)
                .Select(stack => new ItemStackMessage { ItemId = stack.ItemId, Quantity = stack.Quantity })
                .ToArray();
        }

        public ulong RequestGiftInventory()
        {
            if (!HasAuthenticatedClient)
                return 0;
            var requestId = ++_nextGiftRequest;
            NetworkClient.Send(new GiftInventoryRequest { RequestId = requestId });
            return requestId;
        }

        public string RequestGiftUseBatch(GiftUseCommandMessage[] commands)
        {
            if (!HasAuthenticatedClient || commands == null || commands.Length == 0)
                return null;
            var operationId = Guid.NewGuid().ToString("N");
            InvalidateCollection();
            NetworkClient.Send(new GiftUseBatchRequest
            {
                RequestId = ++_nextGiftRequest,
                OperationId = operationId,
                Commands = commands,
            });
            return operationId;
        }

        private void RegisterGiftClientHandlers()
        {
            NetworkClient.RegisterHandler<GiftInventoryResponse>(response =>
                GiftInteractionController.EnsureActive()?.ReceiveInventory(response));
            NetworkClient.RegisterHandler<GiftUseBatchResponse>(response =>
                GiftInteractionController.EnsureActive()?.ReceiveUseResponse(response));
            NetworkClient.RegisterHandler<GiftPlaybackBatchMessage>(message =>
                GiftInteractionController.EnsureActive()?.ReceivePlayback(message));
        }

        private void ClearGiftServer()
        {
            _giftUseRequests.Clear();
            _giftInventoryRequests.Clear();
        }

        private void ClearGiftClient()
        {
            _nextGiftRequest = 0;
            GiftInteractionController.Active?.ResetSession();
        }
    }
}
