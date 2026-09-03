using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Velvet;

internal enum VelvetFilterFacet
{
    Region,
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

    private readonly string[] filterSummaries = new string[FilterFacets.Length];

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
            filterSummaries[index] = SummaryFor(FilterFacets[index], include);
        }
    }

    private string SummaryFor(VelvetFilterFacet facet, VelvetFilterSelection include)
    {
        if (facet == VelvetFilterFacet.Region)
        {
            return include.Region.Length > 0 ? include.Region : Loc.T(L.Velvet.FilterAny);
        }

        var shown = 0;
        var hidden = 0;
        switch (facet)
        {
            case VelvetFilterFacet.Race:
                shown = VelvetRace.Count(include.Race);
                hidden = VelvetRace.Count(mutes.Race);
                break;
            case VelvetFilterFacet.Intent:
                shown = MaskCount(include.Intent);
                hidden = MaskCount(mutes.Intent);
                break;
            case VelvetFilterFacet.Gender:
                shown = MaskCount(include.Gender);
                hidden = MaskCount(mutes.Gender);
                break;
            case VelvetFilterFacet.Sexuality:
                shown = MaskCount(include.Sexuality);
                hidden = MaskCount(mutes.Sexuality);
                break;
            case VelvetFilterFacet.Relationship:
                shown = MaskCount(include.Relationship);
                hidden = MaskCount(mutes.Relationship);
                break;
            case VelvetFilterFacet.Role:
                shown = include.Roles.Count;
                hidden = mutes.Roles.Count;
                break;
            case VelvetFilterFacet.Kinks:
                shown = include.Kinks.Count;
                hidden = mutes.Kinks.Count;
                break;
            case VelvetFilterFacet.Limits:
                shown = include.Limits.Count;
                hidden = mutes.Limits.Count;
                break;
            default:
                shown = include.Tags.Count;
                hidden = mutes.Tags.Count;
                break;
        }

        if (shown == 0 && hidden == 0)
        {
            return Loc.T(L.Velvet.FilterAny);
        }

        if (hidden == 0)
        {
            return Loc.T(L.Velvet.FilterShownCount, shown);
        }

        return shown == 0
            ? Loc.T(L.Velvet.FilterHiddenCount, hidden)
            : Loc.T(L.Velvet.FilterShownHidden, shown, hidden);
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
