namespace Game.Network.Accounts
{
    public enum CreateAccountResult
    {
        Created,
        UserNameTaken,
    }

    public enum ChipMutationResult
    {
        Applied,
        AlreadyApplied,
        AccountNotFound,
        InsufficientChips,
        Overflow,
    }

    public interface IAccountRepository
    {
        void Initialize();

        PlayerAccount FindById(string accountId);

        PlayerAccount FindByUserName(string userName);

        CreateAccountResult Create(PlayerAccount account);

        bool SetLastRoom(string accountId, int? roomId);

        ChipMutationResult ApplyChipMutation(
            string accountId,
            string requestId,
            string transactionType,
            long amount,
            int? roomId,
            string handId,
            out PlayerAccount account);
    }
}
