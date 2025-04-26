using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class PrivateAttributeResponse
{    
    [JsonPropertyName("id")] 
    public int? Id { get; set; }
    
    [JsonPropertyName("type")] 
    public string? Type { get; set; }
    
    [JsonPropertyName("attribute")] 
    public string? Attribute { get; set; }
    
    [JsonPropertyName("op")] 
    public string? OP { get; set; }
    
    [JsonPropertyName("value")] 
    public string? Value { get; set; }
    
    [JsonPropertyName("edit")] 
    public bool Edit { get; set; }
    
    [JsonPropertyName("delete")] 
    public bool Delete { get; set; }
}
