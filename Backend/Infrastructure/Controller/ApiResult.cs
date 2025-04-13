using System.Reflection;

namespace PhotonBypass.Infra.Controller;

public class ApiResult
{
    public short Code { get; set; }

    public string? Message { get; set; }

    public string? Developer { get; set; }

    public MessageMethod? MessageMethod { get; set; }

    public static ApiResult Success(string message)
    {
        return new ApiResult
        {
            Code = 200,
            Message = message,
        };
    }
}

public class ApiResult<Model> : ApiResult
{
    public Model? Data { get; set; }

    public static ApiResult<Model> Success(Model model)
    {
        return new ApiResult<Model>
        {
            Code = 200,
            Data = model,
        };
    }
}