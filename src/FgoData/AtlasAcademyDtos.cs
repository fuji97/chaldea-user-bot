using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FgoData;

internal sealed record BasicServant(
    [property: JsonPropertyName("collectionNo")] int CollectionNo,
    [property: JsonPropertyName("name")] string Name);

internal sealed record NiceServant(
    [property: JsonPropertyName("collectionNo")] int CollectionNo,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("rarity")] int Rarity,
    [property: JsonPropertyName("atkBase")] int AtkBase,
    [property: JsonPropertyName("atkMax")] int AtkMax,
    [property: JsonPropertyName("hpBase")] int HpBase,
    [property: JsonPropertyName("hpMax")] int HpMax,
    [property: JsonPropertyName("cards")] string[]? Cards,
    [property: JsonPropertyName("noblePhantasms")] NiceNoblePhantasm[]? NoblePhantasms,
    [property: JsonPropertyName("profile")] NiceProfile? Profile,
    [property: JsonPropertyName("extraAssets")] NiceExtraAssets? ExtraAssets);

internal sealed record NiceNoblePhantasm(
    [property: JsonPropertyName("card")] string Card);

internal sealed record NiceProfile(
    [property: JsonPropertyName("comments")] NiceProfileComment[]? Comments);

internal sealed record NiceProfileComment(
    [property: JsonPropertyName("comment")] string? Comment);

internal sealed record NiceExtraAssets(
    [property: JsonPropertyName("charaGraph")] NiceAssetSet? CharaGraph,
    [property: JsonPropertyName("faces")] NiceAssetSet? Faces);

internal sealed record NiceAssetSet(
    [property: JsonPropertyName("ascension")] Dictionary<string, string>? Ascension);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(BasicServant[]))]
[JsonSerializable(typeof(NiceServant))]
internal sealed partial class FgoDataJsonContext : JsonSerializerContext {
}
