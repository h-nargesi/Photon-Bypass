using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.Test.MockOutSources.Models;
using System.Text.Json.Serialization;

namespace PhotonBypass.Test.MockFreeRadius.Models;

class PermanentUserMoqModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    [JsonConverter(typeof(DateTimeNullableJsonConverter))]
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
