using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Entity;

/// <summary>
/// Pricing group (Docs/24 D6): the commercial access axis that decides which
/// plans a member can purchase. Invisible to members by itself — only its
/// effect (the offered plans/prices) is visible. Admin-only CRUD.
/// </summary>
[Table("UserGroup")]
public class UserGroupEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Admin-facing title; unique.</summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// The «وب سایت» group: landing point of self-registration and of
    /// admin-created independent users. At most one (filtered unique index).
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}
