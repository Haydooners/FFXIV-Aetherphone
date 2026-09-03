using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
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

    private static int FacetOptionCount(VelvetFilterFacet facet) =>
        facet switch
        {
            VelvetFilterFacet.Region => SocialRegion.Codes.Length,
            VelvetFilterFacet.Race => VelvetRace.All.Length,
            VelvetFilterFacet.Intent => VelvetIntent.All.Length,
            VelvetFilterFacet.Gender => VelvetGender.All.Length,
            VelvetFilterFacet.Sexuality => VelvetSexuality.All.Length,
            VelvetFilterFacet.Relationship => VelvetRelationship.All.Length,
            VelvetFilterFacet.Role => VelvetSuggestions.Roles.Length,
            VelvetFilterFacet.Kinks => VelvetSuggestions.Kinks.Length,
            VelvetFilterFacet.Limits => VelvetSuggestions.Limits.Length,
            _ => TagOptionCount(),
        };

    private static int TagOptionCount()
    {
        var categories = VelvetSuggestions.TagCategories;
        var count = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            count += categories[index].Tags.Length;
        }

        return count;
    }

    private static float FacetContentHeight(VelvetFilterFacet facet)
    {
        var rows = FacetOptionCount(facet) * FilterOptionRowHeight + FilterRevealPadY;
        return facet == VelvetFilterFacet.Tags
            ? rows + VelvetSuggestions.TagCategories.Length * FilterGroupHeaderHeight
            : rows;
    }

    private void FillFacetLabels(VelvetFilterFacet facet)
    {
        facetLabels.Clear();
        switch (facet)
        {
            case VelvetFilterFacet.Region:
                for (var index = 0; index < SocialRegion.Codes.Length; index++)
                {
                    facetLabels.Add(SocialRegion.Codes[index]);
                }

                break;
            case VelvetFilterFacet.Race:
                for (var index = 0; index < VelvetRace.All.Length; index++)
                {
                    facetLabels.Add(VelvetRace.Label(gameData, VelvetRace.All[index]));
                }

                break;
            case VelvetFilterFacet.Intent:
                for (var index = 0; index < VelvetIntent.All.Length; index++)
                {
                    facetLabels.Add(Loc.T(VelvetIntent.All[index].Label));
                }

                break;
            case VelvetFilterFacet.Gender:
                for (var index = 0; index < VelvetGender.All.Length; index++)
                {
                    facetLabels.Add(VelvetGender.Label(VelvetGender.All[index]));
                }

                break;
            case VelvetFilterFacet.Sexuality:
                for (var index = 0; index < VelvetSexuality.All.Length; index++)
                {
                    facetLabels.Add(VelvetSexuality.Label(VelvetSexuality.All[index]));
                }

                break;
            case VelvetFilterFacet.Relationship:
                for (var index = 0; index < VelvetRelationship.All.Length; index++)
                {
                    facetLabels.Add(VelvetRelationship.Label(VelvetRelationship.All[index]));
                }

                break;
            case VelvetFilterFacet.Role:
                CopyLabels(VelvetSuggestions.Roles);
                break;
            case VelvetFilterFacet.Kinks:
                CopyLabels(VelvetSuggestions.Kinks);
                break;
            case VelvetFilterFacet.Limits:
                CopyLabels(VelvetSuggestions.Limits);
                break;
        }
    }

    private void CopyLabels(string[] source)
    {
        for (var index = 0; index < source.Length; index++)
        {
            facetLabels.Add(source[index]);
        }
    }

    private VelvetOptionState OptionState(VelvetFilterFacet facet, VelvetFilterSelection include, int optionIndex)
    {
        if (facet == VelvetFilterFacet.Region)
        {
            return (include.RegionMask & (1 << optionIndex)) != 0 ? VelvetOptionState.Shown : VelvetOptionState.Any;
        }

        if (TokenFacet(facet, out var catalog))
        {
            var token = catalog[optionIndex];
            if (TokensFor(facet, mutes).Contains(token))
            {
                return VelvetOptionState.Hidden;
            }

            return TokensFor(facet, include).Contains(token) ? VelvetOptionState.Shown : VelvetOptionState.Any;
        }

        var flag = FacetFlag(facet, optionIndex);
        if ((MaskFor(facet, mutes) & flag) != 0)
        {
            return VelvetOptionState.Hidden;
        }

        return (MaskFor(facet, include) & flag) != 0 ? VelvetOptionState.Shown : VelvetOptionState.Any;
    }

    private void SetOptionState(VelvetFilterFacet facet, VelvetFilterSelection include, int optionIndex,
        VelvetOptionState state)
    {
        if (facet == VelvetFilterFacet.Region)
        {
            var bit = 1 << optionIndex;
            var on = (include.RegionMask & bit) != 0;
            if (state == VelvetOptionState.Shown != on)
            {
                include.RegionMask = SocialRegion.ToggleMask(include.RegionMask, optionIndex);
            }

            return;
        }

        if (TokenFacet(facet, out var catalog))
        {
            var token = catalog[optionIndex];
            TokensFor(facet, include).Remove(token);
            TokensFor(facet, mutes).Remove(token);
            if (state == VelvetOptionState.Shown)
            {
                TokensFor(facet, include).Add(token);
            }
            else if (state == VelvetOptionState.Hidden)
            {
                TokensFor(facet, mutes).Add(token);
            }

            return;
        }

        var flag = FacetFlag(facet, optionIndex);
        SetMask(facet, include, MaskFor(facet, include) & ~flag);
        SetMask(facet, mutes, MaskFor(facet, mutes) & ~flag);
        if (state == VelvetOptionState.Shown)
        {
            SetMask(facet, include, MaskFor(facet, include) | flag);
        }
        else if (state == VelvetOptionState.Hidden)
        {
            SetMask(facet, mutes, MaskFor(facet, mutes) | flag);
        }
    }

    private string FacetSummary(VelvetFilterFacet facet, VelvetFilterSelection include)
    {
        var shown = 0;
        var hidden = 0;
        var count = FacetOptionCount(facet);
        for (var index = 0; index < count; index++)
        {
            switch (OptionState(facet, include, index))
            {
                case VelvetOptionState.Shown:
                    shown++;
                    break;
                case VelvetOptionState.Hidden:
                    hidden++;
                    break;
            }
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

    private static bool TokenFacet(VelvetFilterFacet facet, out string[] catalog)
    {
        switch (facet)
        {
            case VelvetFilterFacet.Role:
                catalog = VelvetSuggestions.Roles;
                return true;
            case VelvetFilterFacet.Kinks:
                catalog = VelvetSuggestions.Kinks;
                return true;
            case VelvetFilterFacet.Limits:
                catalog = VelvetSuggestions.Limits;
                return true;
            case VelvetFilterFacet.Tags:
                catalog = TagCatalog;
                return true;
            default:
                catalog = Array.Empty<string>();
                return false;
        }
    }

    private static readonly string[] TagCatalog = BuildTagCatalog();

    private static string[] BuildTagCatalog()
    {
        var categories = VelvetSuggestions.TagCategories;
        var catalog = new string[TagOptionCount()];
        var cursor = 0;
        for (var index = 0; index < categories.Length; index++)
        {
            var tags = categories[index].Tags;
            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                catalog[cursor++] = tags[tagIndex];
            }
        }

        return catalog;
    }

    private static HashSet<string> TokensFor(VelvetFilterFacet facet, VelvetFilterSelection selection) =>
        facet switch
        {
            VelvetFilterFacet.Role => selection.Roles,
            VelvetFilterFacet.Kinks => selection.Kinks,
            VelvetFilterFacet.Limits => selection.Limits,
            _ => selection.Tags,
        };

    private static int FacetFlag(VelvetFilterFacet facet, int optionIndex) =>
        facet switch
        {
            VelvetFilterFacet.Race => VelvetRace.Bit(VelvetRace.All[optionIndex]),
            VelvetFilterFacet.Intent => VelvetIntent.All[optionIndex].Flag,
            VelvetFilterFacet.Gender => VelvetGender.All[optionIndex],
            VelvetFilterFacet.Sexuality => VelvetSexuality.All[optionIndex],
            VelvetFilterFacet.Relationship => 1 << VelvetRelationship.All[optionIndex],
            _ => 0,
        };

    private static int MaskFor(VelvetFilterFacet facet, VelvetFilterSelection selection) =>
        facet switch
        {
            VelvetFilterFacet.Race => selection.Race,
            VelvetFilterFacet.Intent => selection.Intent,
            VelvetFilterFacet.Gender => selection.Gender,
            VelvetFilterFacet.Sexuality => selection.Sexuality,
            VelvetFilterFacet.Relationship => selection.Relationship,
            _ => 0,
        };

    private static void SetMask(VelvetFilterFacet facet, VelvetFilterSelection selection, int mask)
    {
        switch (facet)
        {
            case VelvetFilterFacet.Race:
                selection.Race = mask;
                break;
            case VelvetFilterFacet.Intent:
                selection.Intent = mask;
                break;
            case VelvetFilterFacet.Gender:
                selection.Gender = mask;
                break;
            case VelvetFilterFacet.Sexuality:
                selection.Sexuality = mask;
                break;
            case VelvetFilterFacet.Relationship:
                selection.Relationship = mask;
                break;
        }
    }
}
