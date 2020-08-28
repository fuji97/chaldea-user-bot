using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Rayshift.Utils;

namespace Rayshift.Models {
    public class ApiMaster {
        [JsonPropertyName("lastUpdate")]
        [JsonConverter(typeof(TimestampConverter))]
        public DateTimeOffset LastUpdate { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("code")]
        public string Code { get; set; }
        [JsonPropertyName("lastLogin")]
        [JsonConverter(typeof(TimestampConverter))]
        public DateTimeOffset LastLogin { get; set; }
        [JsonPropertyName("decks")] 
        public Dictionary<string, string> Decks { get; set; }
        public string BaseAddress { get; set; }

        public string? ImagesBaseUrl => GetImagesBaseUrl();

        public string SupportList(SupportListType supportListType, bool transparent = true) {
            var url = BaseAddress + ImagesBaseUrl;
            switch (supportListType) {
                case SupportListType.Normal:
                    url += "normal";
                    break;
                case SupportListType.Event:
                    url += "event";
                    break;
                case SupportListType.Both:
                    url += "both";
                    break;
            }

            if (transparent) {
                url += "_t";
            }

            return url + ".png";
        }

        private string GetImagesBaseUrl() {
            var str = Decks.FirstOrDefault().Value;
            return str.Substring(0, str.LastIndexOf('/') + 1);
        }
    }
}