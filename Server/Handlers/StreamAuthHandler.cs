using System.Web;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Auth;
using LiveStreamingServerNet.Rtmp.Server.Auth.Contracts;
using Microsoft.EntityFrameworkCore;
using Sharenima.Server.Data;
using Sharenima.Server.Models;

namespace Sharenima.Server.Handlers;

public class StreamAuthHandler : IAuthorizationHandler {
    private ApplicationDbContext ApplicationDbContext { get; set; }

    public StreamAuthHandler(ApplicationDbContext applicationDbContext) {
        ApplicationDbContext = applicationDbContext;
    }

    public async Task<AuthorizationResult> AuthorizePublishingAsync(ISessionInfo client, string streamPath, IReadOnlyDictionary<string, string> streamArguments, string publishingType) {
        if (!streamPath.Contains('?')) {
            return AuthorizationResult.Unauthorized("Wrong password");
        }
        string[] argumentSplit = streamPath.Split('?');
        if (argumentSplit.Length < 2) {
            return AuthorizationResult.Unauthorized("Wrong password");
        }
        
        var password = HttpUtility.ParseQueryString(streamPath.Split('?')[1]).Get("password");
        if (password == null) {
            return AuthorizationResult.Unauthorized("Wrong password");
        }
        
        string username = argumentSplit[0].Split('/').Last();
        ApplicationUser? user = await ApplicationDbContext.Users.FirstOrDefaultAsync(user => user.UserName == username);
        if (user == null || user.StreamKey != password) {
            return AuthorizationResult.Unauthorized("Wrong password");
        }
        
        return AuthorizationResult.Authorized();
    }

    public Task<AuthorizationResult> AuthorizeSubscribingAsync(ISessionInfo client, string streamPath, IReadOnlyDictionary<string, string> streamArguments) {
        return Task.FromResult(AuthorizationResult.Authorized());
    }
}