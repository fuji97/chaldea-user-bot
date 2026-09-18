namespace FgoData;

public sealed record ServantDetails(
    int CollectionNo, string Name, string ClassName, int Rarity,
    int AtkBase, int AtkMax, int HpBase, int HpMax,
    string Deck, string NpType, string? Comment, string? ImageUrl, string DbUrl);
