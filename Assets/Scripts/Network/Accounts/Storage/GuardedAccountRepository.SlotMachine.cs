namespace Game.Network.Accounts
{
    public sealed partial class GuardedAccountRepository : ISlotLotteryRepository
    {
        public SlotDrawExecution DrawSlot(string accountId, string operationId, string revision, SlotLotteryCatalog catalog, GameCatalog items, int count = 1)
            => Execute(() => ((ISlotLotteryRepository)_inner).DrawSlot(accountId, operationId, revision, catalog, items, count));
        public SlotDrawReceipt ReadPendingSlot(string accountId)
            => Execute(() => ((ISlotLotteryRepository)_inner).ReadPendingSlot(accountId));
        public void AcknowledgeSlot(string accountId, string operationId)
            => Execute(() => { ((ISlotLotteryRepository)_inner).AcknowledgeSlot(accountId, operationId); return true; });
        public int ReadSlotMisses(string accountId)
            => Execute(() => ((ISlotLotteryRepository)_inner).ReadSlotMisses(accountId));
    }
}
