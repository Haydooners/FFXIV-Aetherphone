using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetFilterFacet
{
    None = -1,
    Region = 0,
    Race,
    Intent,
    Gender,
    Sexuality,
    Relationship,
    Role,
    Kinks,
    Limits,
    Tags,
}

internal sealed partial class VelvetShell
{
    private static readonly VelvetFilterFacet[] FilterFacets =
    {
        VelvetFilterFacet.Region,
        VelvetFilterFacet.Race,
        VelvetFilterFacet.Intent,
        VelvetFilterFacet.Gender,
        VelvetFilterFacet.Sexuality,
        VelvetFilterFacet.Relationship,
        VelvetFilterFacet.Role,
        VelvetFilterFacet.Kinks,
        VelvetFilterFacet.Limits,
        VelvetFilterFacet.Tags,
    };

    private static readonly VelvetFilterFacet[] HiddenFacets =
    {
        VelvetFilterFacet.Kinks,
        VelvetFilterFacet.Limits,
        VelvetFilterFacet.Tags,
    };

    private readonly string[] filterSummaries = new string[FilterFacets.Length];
    private readonly string[] hiddenSummaries = new string[HiddenFacets.Length];

    private static LocString FacetTitle(VelvetFilterFacet facet) =>
        facet switch
        {
            VelvetFilterFacet.Region => L.Velvet.RegionLabel,
            VelvetFilterFacet.Race => L.Velvet.CardRace,
            VelvetFilterFacet.Intent => L.Velvet.CardIntent,
            VelvetFilterFacet.Gender => L.Velvet.CardGender,
            VelvetFilterFacet.Sexuality => L.Velvet.CardSexuality,
            VelvetFilterFacet.Relationship => L.Velvet.CardRelationship,
            VelvetFilterFacet.Role => L.Velvet.CardRole,
            VelvetFilterFacet.Kinks => L.Velvet.CardKinks,
            VelvetFilterFacet.Limits => L.Velvet.CardLimits,
            _ => L.Velvet.CardTags,
        };

    private void RefreshFilterSummaries()
    {
        var include = IncludeFor(filterSurface);
        for (var index = 0; index < FilterFacets.Length; index++)
        {
            filterSummaries[index] = SummaryFor(FilterFacets[index], include, false);
        }
    }

    private void RefreshHiddenSummaries()
    {
        for (var index = 0; index < HiddenFacets.Length; index++)
        {
            hiddenSummaries[index] = SummaryFor(HiddenFacets[index], mutes, true);
        }
    }

    private string HiddenSummary()
    {
        var count = 0;
        for (var index = 0; index < HiddenFacets.Length; index++)
        {
            count += CountFor(HiddenFacets[index], mutes);
        }

        return count == 0 ? Loc.T(L.Velvet.FilterAny) : Loc.T(L.Velvet.FilterHiddenCount, count);
    }

    private string SummaryFor(VelvetFilterFacet facet, VelvetFilterSelection target, bool hiding)
    {
        if (facet == VelvetFilterFacet.Region)
        {
            var regions = RegionCount(target.RegionMask);
            return regions == 0 ? Loc.T(L.Velvet.FilterAny) : Loc.T(L.Velvet.FilterSelectedCount, regions);
        }

        var count = CountFor(facet, target);
        if (count == 0)
        {
            return Loc.T(L.Velvet.FilterAny);
        }

        return hiding ? Loc.T(L.Velvet.FilterHiddenCount, count) : Loc.T(L.Velvet.FilterSelectedCount, count);
    }

    private static int CountFor(VelvetFilterFacet facet, VelvetFilterSelection target) =>
        facet switch
        {
            VelvetFilterFacet.Race => VelvetRace.Count(target.Race),
            VelvetFilterFacet.Intent => MaskCount(target.Intent),
            VelvetFilterFacet.Gender => MaskCount(target.Gender),
            VelvetFilterFacet.Sexuality => MaskCount(target.Sexuality),
            VelvetFilterFacet.Relationship => MaskCount(target.Relationship),
            VelvetFilterFacet.Role => target.Roles.Count,
            VelvetFilterFacet.Kinks => target.Kinks.Count,
            VelvetFilterFacet.Limits => target.Limits.Count,
            VelvetFilterFacet.Tags => target.Tags.Count,
            _ => 0,
        };

    private static int RegionCount(int mask)
    {
        if (mask == 0)
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; index < SocialRegion.Codes.Length; index++)
        {
            if ((mask & (1 << index)) != 0)
            {
                count++;
            }
        }

        return count == SocialRegion.Codes.Length ? 0 : count;
    }

    private static int MaskCount(int mask)
    {
        var count = 0;
        while (mask != 0)
        {
            mask &= mask - 1;
            count++;
        }

        return count;
    }
}
