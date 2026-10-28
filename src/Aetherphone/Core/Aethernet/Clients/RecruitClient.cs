using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class RecruitClient
{
    private readonly AethernetTransport net;

    public RecruitClient(AethernetTransport net)
    {
        this.net = net;
    }

    public Task<RecruitListingDto[]?> ListAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.GetAsync("/recruit/listings",
        AethernetJsonContext.Default.RecruitListingDtoArray, token, null, onFailure);
    }

    public Task<RecruitListingDto?> CreateAsync(CreateRecruitRequest request, 
    CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/recruit/listings", request, AethernetJsonContext.Default.CreateRecruitRequest,
        AethernetJsonContext.Default.RecruitListingDto, token, null, onFailure);
    }

    public Task<PfExtrasDto?> PublishPfExtrasAsync(PublishPfExtrasRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.SendJsonAsync(HttpMethod.Put, "/recruit/pf-extras", request,
            AethernetJsonContext.Default.PublishPfExtrasRequest, AethernetJsonContext.Default.PfExtrasDto,
            token, null, onFailure);
    }

    public Task<bool> ClearPfExtrasAsync(string contentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return net.SendAsync(HttpMethod.Delete, "/recruit/pf-extras/" + Uri.EscapeDataString(contentId),
            token, null, onFailure);
    }

    public Task<PfExtrasDto[]?> LookupPfExtrasAsync(PfExtrasLookupRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return net.PostAsync("/recruit/pf-extras/lookup", request,
            AethernetJsonContext.Default.PfExtrasLookupRequest, AethernetJsonContext.Default.PfExtrasDtoArray,
            token, null, onFailure);
    }
}
