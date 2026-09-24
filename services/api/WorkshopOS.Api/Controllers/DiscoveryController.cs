using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopOS.Contracts.Common;
using WorkshopOS.Infrastructure.Persistence;

namespace WorkshopOS.Api.Controllers;

[ApiController]
public sealed class DiscoveryController : ControllerBase
{
    private readonly WorkshopDbContext _db;
    public DiscoveryController(WorkshopDbContext db) => _db = db;

    [HttpGet("api/discovery")]
    [AllowAnonymous]
    public async Task<DiscoveryDto> Discovery(CancellationToken ct)
    {
        var code = await EnsurePairingCodeAsync(ct);
        var setup = await DbSeed.GetSettingAsync(_db, SettingKeys.SetupCompleted, false, ct);
        string? business = null;
        try
        {
            var profile = await DbSeed.GetSettingAsync<WorkshopOS.Contracts.Auth.BusinessProfileDto?>(
                _db, SettingKeys.BusinessProfile, null, ct);
            business = profile?.Name;
        }
        catch { /* ignore */ }

        var urls = BuildSuggestedUrls(Request);
        var version = typeof(DiscoveryController).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        return new DiscoveryDto("WorkshopOS", code, setup, version, business, urls);
    }

    [HttpGet("connect")]
    [AllowAnonymous]
    public async Task<ContentResult> ConnectPortal(CancellationToken ct)
    {
        var discovery = await Discovery(ct);
        var primary = discovery.SuggestedUrls.FirstOrDefault() ?? $"{Request.Scheme}://{Request.Host}";
        var html = BuildHtml(discovery, primary);
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpPost("api/discovery/regenerate-code")]
    [Authorize(Policy = "perm:settings.manage")]
    public async Task<DiscoveryDto> Regenerate(CancellationToken ct)
    {
        var code = NewPairingCode();
        await DbSeed.SetSettingAsync(_db, SettingKeys.PairingCode, code, null, ct);
        await _db.SaveChangesAsync(ct);
        return await Discovery(ct);
    }

    private async Task<string> EnsurePairingCodeAsync(CancellationToken ct)
    {
        var existing = await DbSeed.GetSettingAsync(_db, SettingKeys.PairingCode, "", ct);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing.Trim().ToUpperInvariant();

        var code = NewPairingCode();
        await DbSeed.SetSettingAsync(_db, SettingKeys.PairingCode, code, null, ct);
        await _db.SaveChangesAsync(ct);
        return code;
    }

    public static string NewPairingCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[4];
        Random.Shared.NextBytes(bytes);
        var chars = new char[4];
        for (var i = 0; i < 4; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return $"WOS-{new string(chars)}";
    }

    private static IReadOnlyList<string> BuildSuggestedUrls(HttpRequest request)
    {
        var port = request.Host.Port ?? (request.Scheme == "https" ? 443 : 80);
        var urls = new List<string>();
        var hostHeader = $"{request.Scheme}://{request.Host}";
        urls.Add(hostHeader.TrimEnd('/'));

        foreach (var ip in GetLanIPv4Addresses())
        {
            var url = port is 80 or 443
                ? $"{request.Scheme}://{ip}"
                : $"{request.Scheme}://{ip}:{port}";
            if (!urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                urls.Add(url);
        }

        return urls;
    }

    private static IEnumerable<string> GetLanIPv4Addresses()
    {
        var results = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = ua.Address;
                    if (IPAddress.IsLoopback(ip)) continue;
                    results.Add(ip.ToString());
                }
            }
        }
        catch
        {
            /* ignore NIC enumeration failures */
        }
        return results;
    }

    private static string BuildHtml(DiscoveryDto d, string primaryUrl)
    {
        var urls = string.Join("", d.SuggestedUrls.Select(u =>
            $"<li><code>{System.Net.WebUtility.HtmlEncode(u)}</code></li>"));
        var setupLine = d.SetupComplete
            ? $"Shop ready{(string.IsNullOrEmpty(d.BusinessName) ? "" : $": <strong>{System.Net.WebUtility.HtmlEncode(d.BusinessName)}</strong>")} — open the Windows app and sign in."
            : "First PC to connect should complete the setup wizard (business + owner account).";

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Connect WorkshopOS</title>
  <style>
    :root { color-scheme: light; --ink:#0f172a; --muted:#475569; --accent:#0f766e; --bg:#f0fdfa; }
    * { box-sizing: border-box; }
    body { margin:0; font-family: "Segoe UI", system-ui, sans-serif; background:
      radial-gradient(1200px 600px at 10% -10%, #99f6e4 0%, transparent 55%),
      radial-gradient(900px 500px at 100% 0%, #a5f3fc 0%, transparent 50%),
      #f8fafc; color: var(--ink); min-height: 100vh; }
    main { max-width: 560px; margin: 0 auto; padding: 48px 20px 64px; }
    h1 { font-size: 1.75rem; margin: 0 0 8px; letter-spacing: -0.02em; }
    .sub { color: var(--muted); margin-bottom: 28px; line-height: 1.45; }
    .card { background: rgba(255,255,255,0.92); border: 1px solid #e2e8f0; border-radius: 16px;
      padding: 22px 22px 18px; box-shadow: 0 10px 40px rgba(15,23,42,0.06); margin-bottom: 16px; }
    .label { font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--muted); margin-bottom: 8px; }
    .code { font-size: 2.6rem; font-weight: 700; letter-spacing: 0.18em; color: var(--accent);
      font-variant-numeric: tabular-nums; }
    ol { margin: 0; padding-left: 1.2rem; color: var(--muted); line-height: 1.55; }
    li { margin-bottom: 8px; }
    code { background: #ecfeff; padding: 2px 6px; border-radius: 6px; font-size: 0.9em; }
    ul.urls { list-style: none; padding: 0; margin: 8px 0 0; }
    ul.urls li { margin: 6px 0; }
    a { color: var(--accent); }
    .foot { margin-top: 24px; font-size: 0.85rem; color: var(--muted); }
  </style>
</head>
<body>
  <main>
    <h1>WorkshopOS</h1>
    <p class="sub">Server is running. Connect shop PCs with the Windows app — same Wi‑Fi / LAN.</p>

    <div class="card">
      <div class="label">Pairing code</div>
      <div class="code">{{d.PairingCode}}</div>
      <p class="sub" style="margin:12px 0 0">Enter this code in the Windows client, or tap <em>Find on this network</em>.</p>
    </div>

    <div class="card">
      <div class="label">How to connect</div>
      <ol>
        <li>Install the <strong>WorkshopOS</strong> Windows app on a shop PC.</li>
        <li>Open the app → enter pairing code <strong>{{d.PairingCode}}</strong> → Connect.</li>
        <li>Or choose <strong>Find on this network</strong> (same LAN as this server).</li>
        <li>{{setupLine}}</li>
      </ol>
    </div>

    <div class="card">
      <div class="label">Server addresses</div>
      <ul class="urls">{{urls}}</ul>
      <p class="sub" style="margin-top:12px">Manual URL fallback: paste one of these into the client.</p>
    </div>

    <p class="foot">API health: <a href="/api/health">/api/health</a> · Discovery: <a href="/api/discovery">/api/discovery</a> · Primary: <code>{{System.Net.WebUtility.HtmlEncode(primaryUrl)}}</code></p>
  </main>
</body>
</html>
""";
    }
}
