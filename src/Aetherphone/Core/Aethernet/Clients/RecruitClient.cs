using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Aethernet.Clients;

internal sealed class RecruitClient
{
    public const string DefaultBeaconBaseUrl = "http://localhost:4000";

    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly string baseUrl;

    public RecruitClient(HttpService http, AethernetSession session, string baseUrl = DefaultBeaconBaseUrl)
    {
        this.http = http;
        this.session = session;
        this.baseUrl = baseUrl.TrimEnd('/');
    }

    private string Url(string path) => $"{baseUrl}{path}";

    private string? Bearer => session.IsSignedIn ? session.Token : null;

    public Task<RecruitListingDto[]?> ListAsync(CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return http.GetJsonAsync(Url("/recruit/listings"),
            AethernetJsonContext.Default.RecruitListingDtoArray, Bearer, token, null, "recruit", onFailure);
    }

    public Task<RecruitListingDto?> CreateAsync(CreateRecruitRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return http.PostJsonAsync(Url("/recruit/listings"), request, AethernetJsonContext.Default.CreateRecruitRequest,
            AethernetJsonContext.Default.RecruitListingDto, Bearer, token, null, "recruit", onFailure);
    }

    public Task<PfExtrasDto?> PublishPfExtrasAsync(PublishPfExtrasRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return http.SendJsonAsync(HttpMethod.Put, Url("/recruit/pf-extras"), request,
            AethernetJsonContext.Default.PublishPfExtrasRequest, AethernetJsonContext.Default.PfExtrasDto,
            Bearer, token, null, "recruit", onFailure);
    }

    public Task<bool> ClearPfExtrasAsync(string contentId, CancellationToken token,
        Action<AepFailure>? onFailure = null)
    {
        return http.SendAsync(HttpMethod.Delete, Url("/recruit/pf-extras/" + Uri.EscapeDataString(contentId)),
            Bearer, token, null, "recruit", onFailure);
    }

    public Task<PfExtrasDto[]?> LookupPfExtrasAsync(PfExtrasLookupRequest request,
        CancellationToken token, Action<AepFailure>? onFailure = null)
    {
        return http.PostJsonAsync(Url("/recruit/pf-extras/lookup"), request,
            AethernetJsonContext.Default.PfExtrasLookupRequest, AethernetJsonContext.Default.PfExtrasDtoArray,
            Bearer, token, null, "recruit", onFailure);
    }
}
