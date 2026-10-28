using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Recruit;

internal sealed class TomestoneProfileClient
{
    private const string ApiToken = "NOAPIKEYFORYOU";
    private const string BaseApiUrl = "https://tomestone.gg/api";
    private readonly HttpClient http = new();
    private bool requestStarted;
    public bool IsLoading { get; private set; }
    public TomestoneProfileData Profile { get; private set; } = new();

    public void EnsureLoaded(UserDto? user)
    {
        InitLocalPlayerIdentity(user);
        if (string.IsNullOrWhiteSpace(Profile.CharacterName) || string.IsNullOrWhiteSpace(Profile.World))
        {
            return;
        }
        if (requestStarted)
        {
            return;
        }

        requestStarted = true;
        Task.Run(FetchAsync);
    }

    public void LoadCharacter(string characterName, string world)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(world)){
            return;
        }

        if (string.Equals(Profile.CharacterName, characterName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Profile.World, world, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Profile = new TomestoneProfileData
        {
            CharacterName = characterName,
            World = world,
        };

        IsLoading = true;
        Task.Run(FetchAsync);
    }

    private void InitLocalPlayerIdentity(UserDto? user)
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer != null)
        {
            Profile.CharacterName = localPlayer.Name.TextValue;
            Profile.World = localPlayer.HomeWorld.ValueNullable?.Name.ExtractText() ?? string.Empty;
        }
        else if (user is not null)
        {
            Profile.CharacterName = user.Name;
            Profile.World = user.World;
        }
    }

    private static readonly (string Code, string Slug)[] DawntrailExtremes =
    [
        ("VAL", "valigarmanda"),
        ("ZOR", "everkept"),
        ("SPH", "queen-eternal"),
        ("ZEL", "zelenia"),
        ("NEC", "necron"),
        ("DT", "doomtrain"),
        ("ENU", "enuo"),
    ];

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Aetherphone/1.0");
        return request;
    }

    private async Task FetchAsync()
    {
        IsLoading = true;
        try
        {
            var server = Uri.EscapeDataString(Profile.World.Trim());
            var name = Uri.EscapeDataString(Profile.CharacterName.Trim());
            var profileUrl = $"{BaseApiUrl}/character/profile/{server}/{name}";
            using var req = CreateRequest(profileUrl);
            using var res = await http.SendAsync(req);
            if (res.IsSuccessStatusCode)
            {
                var content = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var rootKeys = new List<string>();
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        rootKeys.Add(prop.Name);
                    }
                }
                ParseProfileResponse(doc.RootElement);
            }
            else
            {
                var err = await res.Content.ReadAsStringAsync();
                AepLog.Warning($"[Tomestone] Profile request failed: {(int)res.StatusCode} - {err}");
            }
            await FetchExtremesActivityAsync(server, name);
        }
        catch (Exception ex)
        {
            AepLog.Error(ex, "[Tomestone] Exception in FetchAsync");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task FetchExtremesActivityAsync(string server, string name)
    {
        using var throttle = new System.Threading.SemaphoreSlim(3, 3);
        var tasks = new List<Task>();
        foreach (var (code, slug) in DawntrailExtremes)
        {
            var badge = Profile.Extremes.Find(b => string.Equals(b.Code, code, StringComparison.OrdinalIgnoreCase));
            if (badge != null && badge.IsCleared)
            {
                continue;
            }
            tasks.Add(Task.Run(async () =>
            {
                await throttle.WaitAsync();
                try
                {
                    var url = $"{BaseApiUrl}/character/activity/{server}/{name}?expansion=dawntrail&zone=trials-extreme&encounter={slug}";
                    using var req = CreateRequest(url);
                    using var res = await http.SendAsync(req);
                    if (!res.IsSuccessStatusCode)
                    {
                        var err = await res.Content.ReadAsStringAsync();
                        AepLog.Warning($"[Tomestone] [{code}] Activity query failed: {(int)res.StatusCode} - {err}");
                        return;
                    }
                    var json = await res.Content.ReadAsStringAsync();
                    var preview = json.Length > 200 ? json[..200] + "..." : json;
                    using var doc = JsonDocument.Parse(json);
                    var cleared = HasActivityOrClears(doc.RootElement);
                    if (cleared)
                    {
                        if (badge != null)
                        {
                            badge.IsCleared = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AepLog.Error(ex, $"[Tomestone] [{code}] Activity request failed");
                }
                finally
                {
                    throttle.Release();
                }
            }));
        }
        await Task.WhenAll(tasks);
    }

    private static bool HasActivityOrClears(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.GetArrayLength() > 0;
        }
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (IsEncounterCleared(root))
        {
            return true;
        }
        if (TryGetActivityArray(root, out var array))
        {
            if (array.GetArrayLength() == 0)
            {
                return false;
            }
            foreach (var entry in array.EnumerateArray())
            {
                var act = entry.TryGetProperty("activity", out var nested) ? nested : entry;
                if (IsEncounterCleared(act) || IsEncounterCleared(entry))
                {
                    return true;
                }
            }
            return true;
        }
        return false;
    }

    private static bool TryGetActivityArray(JsonElement root, out JsonElement array)
    {
        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array)
            {
                array = data;
                return true;
            }
            if (data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("paginator", out var paginator) &&
                    paginator.ValueKind == JsonValueKind.Object &&
                    paginator.TryGetProperty("data", out var pData) &&
                    pData.ValueKind == JsonValueKind.Array)
                {
                    array = pData;
                    return true;
                }
                if (data.TryGetProperty("data", out var subData) && subData.ValueKind == JsonValueKind.Array)
                {
                    array = subData;
                    return true;
                }
            }
        }
        if (root.TryGetProperty("results", out var res) && res.ValueKind == JsonValueKind.Array)
        {
            array = res;
            return true;
        }
        array = default;
        return false;
    }

    private void ParseProfileResponse(JsonElement root)
    {
        if (root.TryGetProperty("id", out var idProp))
        {
            Profile.CharacterId = idProp.ToString();
        }
        if (root.TryGetProperty("avatar", out var avatarProp) && avatarProp.ValueKind == JsonValueKind.String)
        {
            Profile.AvatarUrl = avatarProp.GetString();
        }
        if (root.TryGetProperty("externalUrls", out var ext) && ext.ValueKind == JsonValueKind.Object)
        {
            if (ext.TryGetProperty("tomestone", out var tu) && tu.ValueKind == JsonValueKind.String)
            {
                Profile.CustomWebUrl = tu.GetString();
            }
        }
        if (!root.TryGetProperty("encounters", out var encounters))
        {
            return;
        }

        if (encounters.TryGetProperty("savage", out var savageGroups) && savageGroups.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in savageGroups.EnumerateArray())
            {
                var rawName = (group.TryGetProperty("name", out var gn) ? gn.GetString() : null)
                    ?? (group.TryGetProperty("zoneName", out var gzn) ? gzn.GetString() : null)
                    ?? string.Empty;
                var tierName = CleanSavageTierName(rawName);
                var isCleared = false;
                if (group.TryGetProperty("encounters", out var tierEncounters) && tierEncounters.ValueKind == JsonValueKind.Array)
                {
                    foreach (var enc in tierEncounters.EnumerateArray())
                    {
                        if (IsEncounterCleared(enc))
                        {
                            isCleared = true;
                        }
                        else if (Profile.Progression is null)
                        {
                            TryExtractProgression(enc);
                        }
                    }
                }
                else if (IsEncounterCleared(group))
                {
                    isCleared = true;
                }
                var existing = Profile.SavageTiers.Find(t => string.Equals(t.TierName, tierName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    var index = Profile.SavageTiers.IndexOf(existing);
                    Profile.SavageTiers[index] = new TomestoneSavageTier("The Arcadion", tierName, isCleared);
                }
                else
                {
                    Profile.SavageTiers.Add(new TomestoneSavageTier("The Arcadion", tierName, isCleared));
                }
            }
        }
        if (Profile.Progression is null && root.TryGetProperty("progression", out var rootProg) && rootProg.ValueKind == JsonValueKind.Object)
        {
            var boss = (rootProg.TryGetProperty("boss", out var b) ? b.GetString() : null)
                ?? (rootProg.TryGetProperty("name", out var bn) ? bn.GetString() : null);
            var pct = 0f;
            if (rootProg.TryGetProperty("percent", out var pEl) && ReadPercent(pEl) is { } pVal)
            {
                pct = (float)pVal;
            }
            else if (rootProg.TryGetProperty("displayPercent", out var dpEl) && ReadPercent(dpEl) is { } dpVal)
            {
                pct = (float)dpVal;
            }
            if (!string.IsNullOrWhiteSpace(boss) && pct > 0f)
            {
                Profile.Progression = new TomestoneProgFight(boss, pct);
            }
        }
        var clearedUlts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clearedEx = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanEncounters(encounters, clearedUlts, clearedEx);
        foreach (var ult in Profile.Ultimates)
        {
            ult.IsCleared = clearedUlts.Contains(ult.Code);
        }
        foreach (var ex in Profile.Extremes)
        {
            if (clearedEx.Contains(ex.Code))
            {
                ex.IsCleared = true;
            }
        }
    }

    private void TryExtractProgression(JsonElement enc)
    {
        if (!enc.TryGetProperty("progression", out var prog) || prog.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        var hpPercent = 0f;
        if (prog.TryGetProperty("percent", out var pctEl) && ReadPercent(pctEl) is { } pctVal)
        {
            hpPercent = (float)pctVal;
        }
        else if (prog.TryGetProperty("displayPercent", out var dispEl) && ReadPercent(dispEl) is { } dispVal)
        {
            hpPercent = (float)dispVal;
        }
        if (hpPercent <= 0f)
        {
            return;
        }
        var boss = (enc.TryGetProperty("name", out var n) ? n.GetString() : null)
            ?? (enc.TryGetProperty("zoneName", out var zn) ? zn.GetString() : null)
            ?? "Current Fight";
        Profile.Progression = new TomestoneProgFight(boss, hpPercent);
    }

    private static double? ReadPercent(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString()?.TrimEnd('%'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static void ScanEncounters(JsonElement element, HashSet<string> clearedUlts, HashSet<string> clearedEx)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var txt = ExtractSearchableText(element).Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    AepLog.Info($"[Tomestone] Scan encounter entry: '{txt}'");
                }
                CheckUltimate(element, clearedUlts);
                CheckExtreme(element, clearedEx);
                foreach (var prop in element.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "savage", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    ScanEncounters(prop.Value, clearedUlts, clearedEx);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    ScanEncounters(item, clearedUlts, clearedEx);
                }
                break;
        }
    }

    private static string CleanSavageTierName(string raw)
    {
        if (raw.Contains("Light-heavyweight", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Wicked Thunder", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Black Cat", StringComparison.OrdinalIgnoreCase))
        {
            return "Light-Heavyweight";
        }
        if (raw.Contains("Cruiserweight", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Howling Blade", StringComparison.OrdinalIgnoreCase))
        {
            return "Cruiserweight";
        }
        if (raw.Contains("Heavyweight", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Lindwurm", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Vamp Fatale", StringComparison.OrdinalIgnoreCase))
        {
            return "Heavyweight";
        }
        return raw.Replace("(Savage)", "", StringComparison.OrdinalIgnoreCase)
                  .Replace("AAC ", "", StringComparison.OrdinalIgnoreCase)
                  .Trim();
    }

    private static readonly (string Code, string[] Keys)[] UltimateMatches =
    [
        ("UWU", ["Weapon's Refrain", "Ultima Weapon", "Ultimate Legend"]),
        ("UCOB", ["Unending Coil", "Bahamut", "The Legend"]),
        ("TEA", ["Alexander", "When I Ruled", "Perfect Legend"]),
        ("DSR", ["Dragonsong", "Heavens' Legend", "As the Heavens Fall", "Thordan"]),
        ("TOP", ["Omega", "Alpha Legend", "Heart of the Machine"]),
        ("FRU", ["Futures Rewritten", "Eden", "Genesis of Legends", "Tomorrow and Tomorrow"]),
        ("DMU", ["Dancing Mad", "Cruel Legend", "Kefka"]),
    ];

    private static readonly (string Code, string[] Keys)[] ExtremeMatches =
    [
        ("VAL", ["Valigarmanda", "Worqor Lar Dor", "Valigarmastered"]),
        ("ZOR", ["Everkeep", "Zoraal Ja", "Everkept"]),
        ("SPH", ["Sphene", "Queen Eternal", "Long Lived the Queen"]),
        ("ZEL", ["Zelenia", "Recollection"]),
        ("NEC", ["Necron"]),
        ("DT", ["Doom Train", "Doomtrain", "Hell on Rails"]),
        ("ENU", ["Enuo", "The Unmaking"]),
    ];

    private static string? MatchRaidCode(string text, (string Code, string[] Keys)[] matches)
    {
        foreach (var (code, keys) in matches)
        {
            foreach (var key in keys)
            {
                if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    return code;
                }
            }
        }
        return null;
    }

    private static string ExtractSearchableText(JsonElement enc)
    {
        var ach = enc.TryGetProperty("achievement", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;
        return $"{GetStringSafe(enc, "name")} {GetStringSafe(enc, "zoneName")} {GetStringSafe(enc, "slug")} {GetStringSafe(enc, "dutyName")} {GetStringSafe(enc, "encounter")} {GetStringSafe(enc, "title")} {GetStringSafe(enc, "description")} {GetStringSafe(ach, "name")} {GetStringSafe(ach, "description")}";
    }

    private static void CheckUltimate(JsonElement enc, HashSet<string> clearedUlts)
    {
        var text = ExtractSearchableText(enc);
        var code = MatchRaidCode(text, UltimateMatches);
        if (code != null)
        {
            var isCleared = IsEncounterCleared(enc);
            var snippet = text.Length > 100 ? text[..100] + "..." : text;
            if (isCleared)
            {
                clearedUlts.Add(code);
            }
        }
    }

    private static void CheckExtreme(JsonElement enc, HashSet<string> clearedEx)
    {
        var code = MatchRaidCode(ExtractSearchableText(enc), ExtremeMatches);
        if (code != null && IsEncounterCleared(enc))
        {
            clearedEx.Add(code);
        }
    }

    private static string? GetStringSafe(JsonElement el, string propertyName)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
            {
                return n.GetString();
            }
        }
        return null;
    }

    private static bool IsEncounterCleared(JsonElement el)
    {
        if (el.TryGetProperty("activity", out var act))
        {
            if (act.ValueKind == JsonValueKind.Object)
            {
                return true;
            }
            if (act.ValueKind == JsonValueKind.Array && act.GetArrayLength() > 0)
            {
                return true;
            }
            if (act.ValueKind == JsonValueKind.True || act.ValueKind == JsonValueKind.String)
            {
                return true;
            }
            if (act.ValueKind == JsonValueKind.Number && act.GetInt32() > 0)
            {
                return true;
            }
        }

        if (el.TryGetProperty("achievement", out var ach))
        {
            if (ach.ValueKind == JsonValueKind.Object || ach.ValueKind == JsonValueKind.True || ach.ValueKind == JsonValueKind.String)
            {
                return true;
            }
            if (ach.ValueKind == JsonValueKind.Number && ach.GetInt32() > 0)
            {
                return true;
            }
        }

        if (el.TryGetProperty("cleared", out var clr) && (clr.ValueKind == JsonValueKind.True || (clr.ValueKind == JsonValueKind.Number && clr.GetInt32() > 0)))
        {
            return true;
        }
        if (el.TryGetProperty("isCleared", out var isClr) && isClr.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (el.TryGetProperty("completed", out var comp) && (comp.ValueKind == JsonValueKind.True || (comp.ValueKind == JsonValueKind.Number && comp.GetInt32() > 0)))
        {
            return true;
        }
        if (el.TryGetProperty("isCompleted", out var isComp) && isComp.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (el.TryGetProperty("completionWeek", out var cw) && cw.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(cw.GetString()))
        {
            return true;
        }
        if (el.TryGetProperty("date", out var dt) && dt.ValueKind != JsonValueKind.Null && dt.ValueKind != JsonValueKind.Undefined)
        {
            return true;
        }
        if (el.TryGetProperty("clears", out var c) && c.ValueKind == JsonValueKind.Number && c.GetInt32() > 0)
        {
            return true;
        }
        if (el.TryGetProperty("killsCount", out var kc) && kc.ValueKind == JsonValueKind.Number && kc.GetInt32() > 0)
        {
            return true;
        }
        if (el.TryGetProperty("kills", out var k) && k.ValueKind == JsonValueKind.Number && k.GetInt32() > 0)
        {
            return true;
        }
        if (el.TryGetProperty("totalClears", out var tc) && tc.ValueKind == JsonValueKind.Number && tc.GetInt32() > 0)
        {
            return true;
        }
        if (el.TryGetProperty("bestPercent", out var bp) && ReadPercent(bp) is { } bpVal && bpVal >= 0.0)
        {
            return true;
        }
        return false;
    }
}
