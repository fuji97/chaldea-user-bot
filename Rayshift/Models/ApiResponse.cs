using System.Text.Json.Serialization;

namespace Rayshift.Models {
    public class ApiResponse {
        [JsonPropertyName("status")]
        public int Status { get; set; }
        [JsonPropertyName("response")]
        public ApiMaster? Response { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("wait")]
        public int? Wait { get; set; }
    }
}