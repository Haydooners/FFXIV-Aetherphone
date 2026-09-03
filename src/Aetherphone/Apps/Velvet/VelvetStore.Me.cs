using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Social;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetStore
{
    public void EnsureMe()
    {
        ReconcileAccountBadges();
        if (!session.IsSignedIn || me is not null || loadingMe)
        {
            return;
        }

        if (!meGate.TryPass())
        {
            return;
        }

        loadingMe = true;
        var epoch = accountEpoch;
        work.Run("profile load", async token =>
        {
            var status = 0;
            var refusal = AepFailure.None;
            var profile = await client.MeAsync(token, code => status = code, failure => refusal = failure)
                .ConfigureAwait(false);
            if (epoch != accountEpoch)
            {
                return;
            }

            if (profile is not null)
            {
                me = profile;
                accessBlocked = false;
                regionBlocked = false;
            }
            else if (status == 403)
            {
                accessBlocked = true;
                regionBlocked = refusal.Code == FailureCodes.VelvetRegionBlocked;
            }
        }, () => loadingMe = false);
    }

    private void ReconcileAccountBadges()
    {
        var current = me;
        var signedInUser = session.CurrentUser;
        if (current is null || signedInUser is null || current.Badges == signedInUser.Badges)
        {
            return;
        }

        me = current with { Badges = signedInUser.Badges };
    }

    public void AcceptGate(int gateVersion, Action<bool> onComplete)
    {
        work.Run("gate accept", async token =>
        {
            var profile = await client.AcceptGateAsync(gateVersion, token).ConfigureAwait(false);
            if (profile is null)
            {
                return false;
            }

            me = profile;
            return true;
        }, onComplete);
    }

    public void UpdateAvatar(string sourcePath, WallpaperCrop crop, Action<bool> onComplete)
    {
        if (avatarBusy)
        {
            return;
        }

        avatarBusy = true;
        work.Run("avatar update", async token =>
        {
            var result = await AvatarUpload.RunAsync(account, media, sourcePath, crop, token).ConfigureAwait(false);
            avatarFailure = result.Outcome;
            if (!result.Ok)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { AvatarUrl = result.PublicUrl };
            }

            return true;
        }, onComplete, () => avatarBusy = false);
    }

    public void UpdateIdentity(string displayName, string handle, Action<bool> onComplete)
    {
        work.Run("identity update", async token =>
        {
            var request = new UpdateProfileRequest(displayName.Length > 0 ? displayName : null,
                handle.Length > 0 ? handle : null, null);
            var updated = await account.UpdateProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            var current = me;
            if (current is not null)
            {
                me = current with { DisplayName = updated.DisplayName, Handle = updated.Handle };
            }

            return true;
        }, onComplete);
    }

    public void UpdateProfile(UpdateVelvetProfileRequest request, Action<bool> onComplete)
    {
        work.Run("profile update", async token =>
        {
            var updated = await client.UpdateProfileAsync(request, token).ConfigureAwait(false);
            if (updated is null)
            {
                return false;
            }

            me = updated;
            return true;
        }, onComplete);
    }
}
