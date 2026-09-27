using Aetherphone.Core.Localization;
using Lumina.Excel.Sheets;

namespace Aetherphone.Apps.Venues;

internal sealed class VenueDataCenterInfo(string name, int regionId, string[] worlds)
{
    public string Name { get; } = name;
    public int RegionId { get; } = regionId;
    public string[] Worlds { get; } = worlds;
    public HashSet<string> Set { get; } = new(StringComparer.OrdinalIgnoreCase) { name };
}

internal sealed class VenueRegionInfo(int id, LocString label, VenueDataCenterInfo[] dataCenters)
{
    public int Id { get; } = id;
    public LocString Label { get; } = label;
    public VenueDataCenterInfo[] DataCenters { get; } = dataCenters;
    public HashSet<string> Set { get; } = BuildSet(dataCenters);

    private static HashSet<string> BuildSet(VenueDataCenterInfo[] dataCenters)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dataCenters.Length; index++)
        {
            set.Add(dataCenters[index].Name);
        }

        return set;
    }
}

internal static class VenueGeography
{
    private const int JapanId = 1;
    private const int NorthAmericaId = 2;
    private const int EuropeId = 3;
    private const int OceaniaId = 4;

    private static readonly int[] RegionOrder = { NorthAmericaId, EuropeId, OceaniaId, JapanId };

    private static VenueRegionInfo[]? regions;
    private static Dictionary<string, VenueDataCenterInfo>? dataCenterByName;
    private static Dictionary<string, VenueDataCenterInfo>? dataCenterByWorld;

    public static VenueRegionInfo[] Regions => regions ??= Build();

    public static VenueRegionInfo? RegionById(int id)
    {
        var all = Regions;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Id == id)
            {
                return all[index];
            }
        }

        return null;
    }

    public static VenueDataCenterInfo? DataCenter(string name)
    {
        _ = Regions;
        return name.Length > 0 && dataCenterByName!.TryGetValue(name, out var info) ? info : null;
    }

    public static VenueDataCenterInfo? DataCenterOfWorld(string world)
    {
        _ = Regions;
        return world.Length > 0 && dataCenterByWorld!.TryGetValue(world, out var info) ? info : null;
    }

    public static VenueRegionInfo? RegionOfDataCenter(string name) =>
        DataCenter(name) is { } info ? RegionById(info.RegionId) : null;

    private static LocString LabelFor(int regionId) =>
        regionId switch
        {
            JapanId => L.Venues.RegionJapan,
            EuropeId => L.Venues.RegionEurope,
            OceaniaId => L.Venues.RegionOceania,
            _ => L.Venues.RegionNorthAmerica,
        };

    private static VenueRegionInfo[] Build()
    {
        var worldsByDataCenter = new Dictionary<uint, List<string>>();
        foreach (var world in Plugin.DataManager.GetExcelSheet<World>())
        {
            if (!world.IsPublic || world.DataCenter.RowId == 0)
            {
                continue;
            }

            var name = world.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            if (!worldsByDataCenter.TryGetValue(world.DataCenter.RowId, out var list))
            {
                list = new List<string>();
                worldsByDataCenter[world.DataCenter.RowId] = list;
            }

            list.Add(name);
        }

        var byRegion = new Dictionary<int, List<VenueDataCenterInfo>>();
        dataCenterByName = new Dictionary<string, VenueDataCenterInfo>(StringComparer.OrdinalIgnoreCase);
        dataCenterByWorld = new Dictionary<string, VenueDataCenterInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in Plugin.DataManager.GetExcelSheet<WorldDCGroupType>())
        {
            var regionId = (int)group.Region.RowId;
            if (group.RowId == 0 || Array.IndexOf(RegionOrder, regionId) < 0 ||
                !worldsByDataCenter.TryGetValue(group.RowId, out var worlds))
            {
                continue;
            }

            var name = group.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            worlds.Sort(StringComparer.OrdinalIgnoreCase);
            var info = new VenueDataCenterInfo(name, regionId, worlds.ToArray());
            dataCenterByName[name] = info;
            for (var index = 0; index < info.Worlds.Length; index++)
            {
                dataCenterByWorld[info.Worlds[index]] = info;
            }

            if (!byRegion.TryGetValue(regionId, out var list))
            {
                list = new List<VenueDataCenterInfo>();
                byRegion[regionId] = list;
            }

            list.Add(info);
        }

        var result = new List<VenueRegionInfo>(RegionOrder.Length);
        for (var index = 0; index < RegionOrder.Length; index++)
        {
            var regionId = RegionOrder[index];
            if (byRegion.TryGetValue(regionId, out var list))
            {
                result.Add(new VenueRegionInfo(regionId, LabelFor(regionId), list.ToArray()));
            }
        }

        return result.ToArray();
    }
}
