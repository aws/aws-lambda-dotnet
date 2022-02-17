using System.Collections.Immutable;

namespace BlueprintBaseName._1;

/// <summary>
/// Stub implementation that validates input values. A real implementation would check a datastore.
/// </summary>
public static class TypeValidators
{
    public static readonly ImmutableArray<string> VALID_CAR_TYPES = ImmutableArray.Create<string>(new string[] { "economy", "standard", "midsize", "full size", "minivan", "luxury" });
    public static bool IsValidCarType(string carType)
    {
        return VALID_CAR_TYPES.Contains(carType.ToLower());
    }

    public static readonly ImmutableArray<string> VALID_CITES = ImmutableArray.Create<string>(new string[]{"new york", "los angeles", "chicago", "houston", "philadelphia", "phoenix", "san antonio",
                "san diego", "dallas", "san jose", "austin", "jacksonville", "san francisco", "indianapolis",
                "columbus", "fort worth", "charlotte", "detroit", "el paso", "seattle", "denver", "washington dc",
                "memphis", "boston", "nashville", "baltimore", "portland" });

    public static bool IsValidCity(string city)
    {
        return VALID_CITES.Contains(city.ToLower());
    }


    public static readonly ImmutableArray<string> VALID_ROOM_TYPES = ImmutableArray.Create<string>(new string[] { "queen", "king", "deluxe" });

    public static bool IsValidRoomType(string roomType)
    {
        return VALID_ROOM_TYPES.Contains(roomType.ToLower());
    }
}