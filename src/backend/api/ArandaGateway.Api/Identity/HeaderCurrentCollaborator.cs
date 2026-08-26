namespace ArandaGateway.Api.Identity;

public sealed class HeaderCurrentCollaborator(
    IHttpContextAccessor httpContextAccessor) : ICurrentCollaborator
{
    public const string HeaderName = "X-Collaborator-Username";

    public string? Username
    {
        get
        {
            var values = httpContextAccessor
                .HttpContext?
                .Request
                .Headers[HeaderName];

            if (values is not { Count: 1 })
            {
                return null;
            }

            var username = values.Value[0]?.Trim();
            return string.IsNullOrWhiteSpace(username) ? null : username;
        }
    }
}
