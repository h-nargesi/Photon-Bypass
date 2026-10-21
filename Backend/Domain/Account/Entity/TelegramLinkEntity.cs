using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Entity;

/// <summary>
/// One-time Telegram link code: the portal stores it, the bot consumes it
/// (DELETE) when the user opens <c>t.me/{bot}?start={code}</c>. Row lifetime
/// is short — only unexpired, unconsumed codes exist.
/// </summary>
[Table("TelegramLink")]
public class TelegramLinkEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public DateTime ExpireDate { get; set; }

    public string Code { get; init; } = null!;

    public DateTime Created { get; init; } = DateTime.Now;
}
