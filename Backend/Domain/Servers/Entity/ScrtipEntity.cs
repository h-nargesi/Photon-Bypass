using PhotonBypass.Domain.Servers.Types;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Script")]
public class ScriptEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int ProcessId { get; set; }

    public int Order { get; set; }

    public ScriptType Type { get; set; }

    public string Content { get; set; } = null!;

    public string? OutputPattern { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;
}
