using PhotonBypass.Tools;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.Test.Shared;

public class JsonConverterTests
{
    [Fact]
    public void JsonConvertors_Deserialize_NullableDatetime_ShouldDesrilizeNullValue()
    {
        var text = @"
{
    ""Date"": null
}
";
        var data = JsonSerializer.Deserialize<NullableDatetime>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Null(data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableDatetime_ShouldDesrilizeNotNullValue()
    {
        var text = @"
{
    ""Date"": 1748438640324
}
";
        var data = JsonSerializer.Deserialize<NullableDatetime>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Equal(DateTime.Parse("2025-05-28 16:54:00.324"), data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NotNullableDateTime_ShouldDesrilizeNotNullValue()
    {
        var text = @"
{
    ""Date"": 1748438640324
}
";
        var data = JsonSerializer.Deserialize<NotNullableDateTime>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Equal(DateTime.Parse("2025-05-28 16:54:00.324"), data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDesrilizeNullValue()
    {
        var text = @"
{
    ""Value"": null
}
";
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Null(data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDesrilizeLongValue()
    {
        var text = @"
{
    ""Value"": 1748438640324
}
";
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Equal(1748438640324, data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDesrilizeStringValue()
    {
        var text = @"
{
    ""Value"": ""1748438640324""
}
";
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Equal(1748438640324, data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NotNullableLong_ShouldDesrilizeNotNullValue()
    {
        var text = @"
{
    ""Value"": ""1748438640324""
}
";
        var data = JsonSerializer.Deserialize<NotNullableLong>(text);

        Assert.NotNull(data);

        if (data == null) return;

        Assert.Equal(1748438640324, data.Value);
    }

    public class NullableDatetime
    {
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime? Date { get; set; }
    }

    public class NotNullableDateTime
    {
        [JsonConverter(typeof(UnixTimestampConverter))]
        public DateTime Date { get; set; }
    }

    public class NullableLong
    {
        [JsonConverter(typeof(StringNumberConverter))]
        public long? Value { get; set; }
    }

    public class NotNullableLong
    {
        [JsonConverter(typeof(StringNumberConverter))]
        public long Value { get; set; }
    }

}
