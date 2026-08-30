using System.Globalization;

namespace Auth019.Models;

/// <summary>
/// The ISO 3166-1 alpha-2 countries offered on the registration form, each with the
/// currency that country uses.
/// </summary>
/// <remarks>
/// Derived from the runtime's own globalization data rather than a hand-kept list,
/// so it can't drift or carry typos. This needs ICU: under globalization-invariant
/// mode the list comes back empty, so never set <c>InvariantGlobalization</c> on
/// this project — including in the container image used for deployment.
/// </remarks>
public static class Countries
{
    private static readonly Lazy<IReadOnlyList<Country>> Cached = new(Build);

    private static readonly Lazy<IReadOnlyDictionary<string, Country>> ByCode =
        new(() => Cached.Value.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyList<Country> All => Cached.Value;

    public static bool IsKnownCode(string? code) => Find(code) is not null;

    /// <summary>The country for an alpha-2 code, or null when it isn't one we offer.</summary>
    public static Country? Find(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.Value.TryGetValue(code, out var country)
            ? country
            : null;

    /// <summary>
    /// The ISO 4217 currency for a country, or null when the country is unknown —
    /// which is the case for accounts created before a country was collected.
    /// </summary>
    public static string? CurrencyFor(string? countryCode) => Find(countryCode)?.CurrencyCode;

    private static IReadOnlyList<Country> Build() =>
        CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture =>
            {
                // A specific culture can still name a region the runtime won't construct.
                try
                {
                    return new RegionInfo(culture.Name);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            })
            .Where(region => region is { TwoLetterISORegionName.Length: 2 })
            .GroupBy(region => region!.TwoLetterISORegionName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Country(
                group.Key.ToUpperInvariant(),
                group.First()!.EnglishName,
                group.First()!.ISOCurrencySymbol))
            .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public record Country(string Code, string Name, string CurrencyCode);
