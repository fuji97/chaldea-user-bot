using System;
using System.Text.Json;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Rayshift.Utils {
    public class TimestampConverter : System.Text.Json.Serialization.JsonConverter<DateTimeOffset> {
        public override void Write(
            Utf8JsonWriter writer,
            DateTimeOffset dateTimeValue,
            JsonSerializerOptions options) => throw new NotImplementedException();
        
        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => Utils.DateTimeFromTimestamp((long) reader.GetInt64());
    }
}