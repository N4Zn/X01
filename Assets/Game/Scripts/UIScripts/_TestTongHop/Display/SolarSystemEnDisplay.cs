/// <summary>
/// English-language variant of the Solar System answer display.
/// Identical layout — only planet names differ (NameEn instead of NameVi).
/// Set displayMode = SolarSystemEn in the CSV to use this display.
/// </summary>
public class SolarSystemEnDisplay : SolarSystemDisplay
{
    protected override string[] Names => NameEn;
}
