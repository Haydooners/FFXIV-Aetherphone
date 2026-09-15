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
}
