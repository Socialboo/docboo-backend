using Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

/// <summary>Configuration section <c>LocalUser</c>.</summary>
public sealed class LocalUserOptions
{
    public const string SectionName = "LocalUser";

    /// <summary>Owner of all works while there is no authentication.</summary>
    public Guid UserId { get; set; } = new("00000000-0000-0000-0000-000000000001");
}

/// <summary>
/// MVP stand-in for authentication: every request acts as the configured local user.
/// Replace with an implementation backed by the authenticated principal when login is added.
/// </summary>
public sealed class LocalCurrentUser(IOptions<LocalUserOptions> options) : ICurrentUser
{
    public Guid UserId { get; } = options.Value.UserId;
}
