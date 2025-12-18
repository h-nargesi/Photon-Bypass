namespace PhotonBypass.Domain;

public interface IBaseEntity
{
    public int Id { get; set; }

    DateTime Created { get; init; }
}