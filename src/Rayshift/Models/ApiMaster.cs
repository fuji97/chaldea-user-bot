using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Rayshift.Utils;

namespace Rayshift.Models;

public class ApiMaster {
    [JsonPropertyName("lastUpdate")]
    [JsonConverter(typeof(TimestampConverter))]
    public DateTimeOffset LastUpdate { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
    [JsonPropertyName("lastLogin")]
    [JsonConverter(typeof(TimestampConverter))]
    public DateTimeOffset LastLogin { get; set; }
    [JsonPropertyName("guid")]
    public string? Guid { get; set; }
    [JsonPropertyName("decks")] 
    public Dictionary<string, string> Decks { get; set; } = new Dictionary<string, string>();
    public string? BaseAddress { get; set; }

    public string? ImagesBaseUrl => GetImagesBaseUrl();

    public string SupportList(Region region,
        SupportLists supportLists = SupportLists.Normal1 | SupportLists.Normal2 | 
                                    SupportLists.Normal3 | SupportLists.Event1 | 
                                    SupportLists.Event2 | SupportLists.Event3, 
        ImageFlags flags = ImageFlags.None) {
        return Utils.Utils.BuildImageUrl(region, Code!, Guid!, (int) supportLists, (int) flags);
    }

    private string GetImagesBaseUrl() {
        var str = Decks.FirstOrDefault().Value;
        return str.Substring(0, str.LastIndexOf('/') + 1);
    }
}