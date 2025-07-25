﻿using PhotonBypass.Tools;
using System.Text.Json.Serialization;

namespace PhotonBypass.Application.Account.Model;

public class HistoryModel
{
    public int Id { get; set; }

    public string Issuer { get; set; } = null!;

    public string Target { get; set; } = null!;

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime EventTime { get; set; }

    public string EventTimeTitle { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Color { get; set; } = null!;

    public object? Value { get; set; }

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
