using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.Venues;

internal enum VenueScreen : byte
{
    List,
    Filters,
    Detail,
}

internal readonly record struct VenueRoute(VenueScreen Screen, VenueEvent? Venue = null)
{
    public static readonly VenueRoute List = new(VenueScreen.List);
    public static readonly VenueRoute Filters = new(VenueScreen.Filters);

    public static VenueRoute Detail(VenueEvent venue) => new(VenueScreen.Detail, venue);
}
