using UnityEngine;

/// <summary>
/// Model: danh sách hành tinh, state hiện tại (planet đang focus).
/// </summary>
public class SolarSystemModel
{
    public PlanetData[] Planets { get; private set; }
    public PlanetData   FocusedPlanet { get; private set; }

    public SolarSystemModel(PlanetData[] planets)
    {
        Planets = planets;
    }

    public void SetFocus(PlanetData planet) => FocusedPlanet = planet;
    public void ClearFocus()               => FocusedPlanet = null;
}
