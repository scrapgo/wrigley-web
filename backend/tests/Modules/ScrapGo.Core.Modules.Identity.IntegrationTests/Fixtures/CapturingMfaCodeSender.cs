using System.Collections.Concurrent;
using ScrapGo.Core.Modules.Identity.Application.Mfa;

namespace ScrapGo.Core.Modules.Identity.IntegrationTests.Fixtures;

/// <summary>Stands in for the Gmail sender: keeps each sign-in code so a spec can type it in, and never emails anyone.</summary>
public sealed class CapturingMfaCodeSender : IMfaCodeSender
{
    private readonly ConcurrentDictionary<string, string> _latestCodes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When false, sending fails as it does without Gmail configured.</summary>
    public bool Deliver { get; set; } = true;

    /// <summary>The last code sent to this address.</summary>
    public string LatestCodeFor(string email) => _latestCodes[email];

    public Task<bool> SendAsync(MfaCodeMessage message, CancellationToken cancellationToken)
    {
        if (!Deliver)
        {
            return Task.FromResult(false);
        }

        _latestCodes[message.Email] = message.Code;
        return Task.FromResult(true);
    }
}
