using System.Text.Json.Serialization;

namespace Rayshift.Models {
    public partial class ApiResponse {
        public const string InQueue = "in queue";
        public const string Processing = "processing";
        public const string Finished = "finished";
        
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("response")]
        public ApiMaster? Response { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("wait")]
        public int? Wait { get; set; }

        public MessageCode MessageType {
            get {
                return Message switch {
                    InQueue => MessageCode.InQueue,
                    Processing => MessageCode.Processing,
                    Finished => MessageCode.Finished,
                    _ => MessageCode.Other
                };
            }
        }
    }
}