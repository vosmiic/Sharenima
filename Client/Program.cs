using MatBlazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Plk.Blazor.DragDrop;
using Sharenima.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient("auth", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddHttpClient("anonymous", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Sharenima.ServerAPI"));
builder.Services.AddScoped<QueuePlayerService>();
builder.Services.AddScoped<RefreshService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<StreamService>();

builder.Services.AddAuthorizationCore(options => {
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim("Admin"));
    options.AddPolicy("ChangeProgress", policy =>
        policy.RequireClaim("ChangeProgress"));
    options.AddPolicy("UploadVideo", policy =>
        policy.RequireClaim("UploadVideo"));
    options.AddPolicy("AddVideo", policy =>
        policy.RequireClaim("AddVideo"));
    options.AddPolicy("DeleteVideo", policy =>
        policy.RequireClaim("DeleteVideo"));
});

builder.Services.AddApiAuthorization();
builder.Services.AddMatBlazor();
builder.Services.AddMatToaster(config => {
    config.Position = MatToastPosition.BottomRight;
    config.PreventDuplicates = true;
    config.NewestOnTop = true;
    config.ShowCloseButton = true;
    config.MaximumOpacity = 95;
    config.VisibleStateDuration = 3000;
});
builder.Services.AddBlazorDragDrop();

await builder.Build().RunAsync();