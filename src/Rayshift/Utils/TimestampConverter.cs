using System;
using System.Text.Json;

namespace Rayshift.Utils;

public class TimestampConverter : System.Text.Json.Serialization.JsonConverter<DateTimeOffset> {
    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset dateTimeValue,
        JsonSerializerOptions options) => writer.WriteNumberValue(dateTimeValue.ToUnixTimeSeconds());

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());
}
