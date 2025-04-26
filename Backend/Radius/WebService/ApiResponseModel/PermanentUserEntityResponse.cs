using PhotonBypass.Domain;
using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class PermanentUserEntityResponse : IBaseEntity
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = null!;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("surname")]
    public string? Surname { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("realm")]
    public string Realm { get; set; } = null!;

    [JsonPropertyName("realm_id")]
    public int RealmId { get; set; }

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = null!;

    [JsonPropertyName("profile_id")]
    public int ProfileId { get; set; }

    [JsonPropertyName("from_date")]
    public DateTime? FromDate { get; set; }

    [JsonPropertyName("to_date")]
    public DateTime? ToDate { get; set; }

    [JsonPropertyName("cloud_id")]
    public int CloudId { get; set; }

    [JsonPropertyName("last_accept_time")]
    public DateTime? LastAcceptTime { get; set; }
}
