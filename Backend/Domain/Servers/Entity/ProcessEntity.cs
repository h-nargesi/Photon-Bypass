using PhotonBypass.Domain.Servers.Types;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Process")]
public class ProcessEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public OsType OsType { get; set; }

    public List<ScriptEntity>? Enable { get; set; }

    public List<ScriptEntity>? Disable { get; set; }

    public ScriptEntity? Check { get; set; }
}
