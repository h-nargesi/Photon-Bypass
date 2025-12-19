using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.Test.Mock.MockFreeRadius.Model;

class PermanentUserMoqModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public DateTime? LastAcceptTime { get; set; }

    public string Phone { get; set; } = null!;

    public string Email { get; set; } = null!;

    public PermanentUserEntity ToEntity()
    {
        return new PermanentUserEntity
        {
            Id = Id,
            Username = Username,
            LastAcceptTime = LastAcceptTime,
            Phone = Phone,
            Email = Email,
        };
    }
}
