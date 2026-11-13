using System.Text.RegularExpressions;
using Plushies.Api.Domain;

namespace Plushies.Api.Features.Builder;

public record BuilderOption(string Id, string Label, int PriceCents = 0);
public record BuilderSize(string Id, string Label, int HeightCm, decimal Multiplier, int WeightGrams);
public record FabricColor(string Name, string Hex);

/// <summary>
/// Single source of truth for what the plushie builder offers and what it costs.
/// The browser renders these options; the server re-validates and re-prices every design,
/// so a tampered request can never set its own price.
/// </summary>
public static partial class BuilderCatalog
{
    public const int EmbroideryPriceCents = 700;
    public const int EmbroideryMaxLength = 12;

    public static readonly BuilderOption[] Species =
    [
        new("bear", "Bear", 3800),
        new("bunny", "Bunny", 3800),
        new("cat", "Cat", 3800),
        new("frog", "Frog", 4000),
        new("dino", "Dino", 4400),
        new("axolotl", "Axolotl", 4600),
    ];

    public static readonly BuilderSize[] Sizes =
    [
        new("mini", "Mini", 18, 0.7m, 150),
        new("classic", "Classic", 30, 1.0m, 320),
        new("jumbo", "Jumbo", 50, 1.75m, 800),
    ];

    public static readonly FabricColor[] Fabrics =
    [
        new("Tangerine", "#FF8A3D"), new("Bubblegum", "#FF5DA2"), new("Tomato", "#F2553D"),
        new("Lemon", "#FFD84D"), new("Mint", "#36E0B0"), new("Pesto", "#7BC950"),
        new("Sky", "#5CC8FF"), new("Blueberry", "#4A6CF7"), new("Grape", "#8B5CF6"),
        new("Lilac", "#C9B6FF"), new("Cocoa", "#9B6A4A"), new("Oat", "#EBD3A8"),
        new("Cream", "#FFF1D6"), new("Cloud", "#FFFFFF"), new("Ash", "#A9A6B8"), new("Midnight", "#2A2350"),
    ];

    public static readonly BuilderOption[] Eyes =
        [new("button", "Button"), new("sparkle", "Sparkle"), new("sleepy", "Sleepy"), new("stitched", "Stitched")];

    public static readonly BuilderOption[] Mouths =
        [new("smile", "Smile"), new("kitty", "Kitty"), new("tongue", "Blep")];

    public static readonly BuilderOption[] Accessories =
    [
        new("none", "None"), new("bow", "Bow tie", 400), new("scarf", "Scarf", 600),
        new("crown", "Crown", 800), new("glasses", "Glasses", 600), new("flower", "Flower", 400),
    ];

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    /// <summary>Returns null when the design is valid, otherwise a message suitable for the customer.</summary>
    public static string? Validate(PlushieDesign d, string? size = null, string? embroidery = null)
    {
        if (Species.All(x => x.Id != d.Species)) return $"Unknown species '{d.Species}'.";
        if (Eyes.All(x => x.Id != d.Eyes)) return $"Unknown eye style '{d.Eyes}'.";
        if (Mouths.All(x => x.Id != d.Mouth)) return $"Unknown mouth style '{d.Mouth}'.";
        if (Accessories.All(x => x.Id != d.Accessory)) return $"Unknown accessory '{d.Accessory}'.";
        foreach (var hex in new[] { d.Body, d.Belly, d.AccessoryColor })
            if (!HexColor().IsMatch(hex)) return $"'{hex}' is not a colour like #FF8A3D.";
        if (size is not null && Sizes.All(x => x.Id != size)) return $"Unknown size '{size}'.";
        if (embroidery is { Length: > EmbroideryMaxLength })
            return $"Embroidery can be at most {EmbroideryMaxLength} characters.";
        return null;
    }

    public static (int PriceCents, int WeightGrams) Price(PlushieDesign d, string size, string? embroidery)
    {
        var species = Species.First(x => x.Id == d.Species);
        var s = Sizes.First(x => x.Id == size);
        var accessory = Accessories.First(x => x.Id == d.Accessory);
        // Round the scaled base to a whole dollar so prices never look like $26.60.
        var basePrice = (int)Math.Round(species.PriceCents * s.Multiplier / 100m, MidpointRounding.AwayFromZero) * 100;
        var total = basePrice + accessory.PriceCents + (string.IsNullOrWhiteSpace(embroidery) ? 0 : EmbroideryPriceCents);
        return (total, s.WeightGrams);
    }
}
