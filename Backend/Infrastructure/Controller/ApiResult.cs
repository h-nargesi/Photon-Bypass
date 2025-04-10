namespace PhotonBypass.Infra.Controller;

public class ApiResult
{
    public short Code { get; set; }

    public string? Message { get; set; }

    public string? Developer { get; set; }

    public MessageMethod? MessageMethod { get; set; }
}

public class ApiResult<Model> : ApiResult
{
    public Model? Data { get; set; }
}