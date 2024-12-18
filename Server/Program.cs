using System.Net;
using LiveStreamingServerNet;
using LiveStreamingServerNet.AdminPanelUI;
using LiveStreamingServerNet.Flv.Installer;
using LiveStreamingServerNet.Standalone;
using LiveStreamingServerNet.Standalone.Installer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Sharenima.Server;
using Sharenima.Server.Data;
using Sharenima.Server.Handlers;
using Sharenima.Server.Models;
using Sharenima.Server.Services;
using Sharenima.Server.SignalR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString), ServiceLifetime.Scoped);
builder.Services.AddDbContextFactory<GeneralDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("General")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddIdentityServer()
    .AddApiAuthorization<ApplicationUser, ApplicationDbContext>();

builder.Services.AddAuthentication()
    .AddIdentityServerJwt();
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IPostConfigureOptions<JwtBearerOptions>,
        ConfigureJwtBearerOptions>());

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.Configure<FormOptions>(x => { x.ValueCountLimit = int.MaxValue; });

if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development") {
    builder.Logging.ClearProviders();
    builder.Logging.AddProvider(new ConsoleLoggerProvider(new ConsoleLoggerConfiguration {
        Colour = ConsoleColor.White,
        LogLevel = LogLevel.Information
    }));
    builder.Logging.AddProvider(new ConsoleLoggerProvider(new ConsoleLoggerConfiguration {
        Colour = ConsoleColor.Red,
        LogLevel = LogLevel.Error
    }));
}

builder.Services.AddSingleton<ConnectionMapping>();

builder.Services.AddScoped<IAuthorizationHandler, ChangeProgressHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UploadVideoHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AddVideoHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdministratorHandler>();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

builder.Services.AddTransient<StreamAuthHandler>();

builder.Services.AddAuthorization(options => {
    options.AddPolicy("Admin", policy =>
        policy.Requirements.Add(new AdministratorRequirement(true)));
    options.AddPolicy("ChangeProgress", policy =>
        policy.Requirements.Add(new ChangeProgressRequirement(true)));
    options.AddPolicy("UploadVideo", policy =>
        policy.Requirements.Add(new UploadVideoRequirement(true)));
    options.AddPolicy("AddVideo", policy =>
        policy.Requirements.Add(new AddVideoRequirement(true)));
    options.AddPolicy("DeleteVideo", policy =>
        policy.Requirements.Add(new DeleteVideoRequirement(true)));
});

builder.Services.AddResponseCompression(opts => {
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

builder.Services.AddLiveStreamingServer(
    new IPEndPoint(IPAddress.Any, 1935),
    options => {
        options.AddAuthorizationHandler(sp => {
            var scopedProvider = sp.CreateScope();
            return scopedProvider.ServiceProvider.GetRequiredService<StreamAuthHandler>();
        });
        options.AddFlv();
    }
);

var app = builder.Build();

app.MapStandaloneServerApiEndPoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
} else {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

var scope = app.Services.CreateScope();
GeneralDbContext context = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(context.Settings.FirstOrDefault(setting => setting.Key == SettingKey.DownloadLocation).Value),
    RequestPath = "/files",
    ServeUnknownFileTypes = true // todo fine for dev, not prod
});

app.UseRouting();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.UseResponseCompression();
app.MapHub<QueueHub>("/queuehub");

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.UseWebSockets();
app.UseWebSocketFlv();

app.UseHttpFlv();

app.Run();