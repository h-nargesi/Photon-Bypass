using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account;

public interface ITelegramLinkRepository
{
    /// <summary>
    /// Atomically consumes (deletes) the row behind a link code; null when
    /// the code is unknown or already used.
    /// </summary>
    Task<TelegramLinkEntity?> Consume(string code);

    Task AddCode(TelegramLinkEntity code);

    /// <summary>
    /// Invalidates every still-active link code of an account before a new
    /// one is issued, so only the newest code can bind the chat.
    /// </summary>
    Task ExpireActiveCodes(int account_id);
}
