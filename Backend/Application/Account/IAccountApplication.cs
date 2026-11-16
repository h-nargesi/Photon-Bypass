using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Account;

public interface IAccountApplication
{
    Task<PanelUserEntity> GetActiveUser(string username);

    Task<ApiResult<UserModel>> GetUser(string username);

    Task<ApiResult<FullUserModel>> GetFullInfo(string target);

    Task<ApiResult> EditUser(string target, EditUserModel context);

    Task<ApiResult> ChangePassword(string target, string token, string password);

    Task<ApiResult> ChangePassword(PanelUserEntity? account, string token, string password);

    Task<ApiResult> ChangePassword(PanelUserEntity? account, string password);

    Task<ApiResult<IList<HistoryModel>>> GetHistory(string target, DateTime? from, DateTime? to);

    /// <summary>
    /// D2: issues a one-time Telegram link code (15 min) for the target
    /// user, retiring its previous pending codes. An already-connected
    /// user returns its state with no code.
    /// </summary>
    Task<ApiResult<TelegramLinkModel>> CreateTelegramLink(string target);

    /// <summary>Whether the target user has a Telegram chat connected.</summary>
    Task<ApiResult<TelegramLinkModel>> GetTelegramLinkStatus(string target);

    /// <summary>Detaches the Telegram chat of the target user, if any.</summary>
    Task<ApiResult> DisconnectTelegram(string target);
}
