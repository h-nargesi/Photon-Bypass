using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.Shared;

public class JsonConverterTests
{
    [Fact]
    public void JsonConvertors_Deserialize_NullableDatetime_ShouldDeserializeNullValue()
    {
        const string text = """
                            {
                                "Date": null
                            }
                            """;
        var data = JsonSerializer.Deserialize<NullableDatetime>(text);

        Assert.NotNull(data);
        Assert.Null(data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableDatetime_ShouldThrowOnInvalidData()
    {
        const string text = """
                            {
                                "Date": "invalid-data"
                            }
                            """;
        var func = () => JsonSerializer.Deserialize<NullableDatetime>(text);
        func.Should().Throw<Exception>().WithMessage("Invalid Unix timestamp.");
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableDatetime_ShouldDeserializeNotNullValue()
    {
        const string text = """
                            {
                                "Date": 1748438640324
                            }
                            """;
        var data = JsonSerializer.Deserialize<NullableDatetime>(text);

        Assert.NotNull(data);
        Assert.Equal(DateTime.Parse("2025-05-28 16:54:00.324"), data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NotNullableDateTime_ShouldDeserializeNotNullValue()
    {
        const string text = """
                            {
                                "Date": 1748438640324
                            }
                            """;
        var data = JsonSerializer.Deserialize<NotNullableDateTime>(text);

        Assert.NotNull(data);
        Assert.Equal(DateTime.Parse("2025-05-28 16:54:00.324"), data.Date);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDeserializeNullValue()
    {
        const string text = """
                            {
                                "Value": null
                            }
                            """;
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);
        Assert.Null(data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDeserializeLongValue()
    {
        const string text = """
                            {
                                "Value": 1748438640324
                            }
                            """;
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);
        Assert.Equal(1748438640324, data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NullableLong_ShouldDeserializeStringValue()
    {
        const string text = """
                            {
                                "Value": "1748438640324"
                            }
                            """;
        var data = JsonSerializer.Deserialize<NullableLong>(text);

        Assert.NotNull(data);
        Assert.Equal(1748438640324, data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NotNullableLong_ShouldDeserializeNotNullValue()
    {
        const string text = """
                            {
                                "Value": "1748438640324"
                            }
                            """;
        var data = JsonSerializer.Deserialize<NotNullableLong>(text);

        Assert.NotNull(data);
        Assert.Equal(1748438640324, data.Value);
    }

    [Fact]
    public void JsonConvertors_Deserialize_NotNullableLong_ShouldThrowOnInvalidData()
    {
        const string text = """
                            {
                                "Value": "invalid-data"
                            }
                            """;
        var func = () => JsonSerializer.Deserialize<NotNullableLong>(text);
        func.Should().Throw<Exception>();
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
