using ArandaGateway.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace ArandaGateway.Api.Tests.Identity;

public sealed class HeaderCurrentCollaboratorTests
{
    [Fact]
    public void Username_ReturnsTrimmedHeaderValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderCurrentCollaborator.HeaderName] =
            "  collaborator  ";
        var accessor = new HttpContextAccessor { HttpContext = context };
        var collaborator = new HeaderCurrentCollaborator(accessor);

        var username = collaborator.Username;

        Assert.Equal("collaborator", username);
    }

    [Fact]
    public void Username_ReturnsNullForMultipleValues()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderCurrentCollaborator.HeaderName] =
            new StringValues(["first", "second"]);
        var accessor = new HttpContextAccessor { HttpContext = context };
        var collaborator = new HeaderCurrentCollaborator(accessor);

        var username = collaborator.Username;

        Assert.Null(username);
    }
}
