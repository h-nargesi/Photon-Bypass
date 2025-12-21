using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Process")]
public class ProcessEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public OperatingSystem OsType { get; set; }

    public List<ScriptEntity>? Enable { get; set; }

    public List<ScriptEntity>? Disable { get; set; }

    public ScriptEntity? Check { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}