using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Sharenima.Server;
using Sharenima.Server.Data;
using Sharenima.Server.Handlers;
using Sharenima.Server.Models;
using Sharenima.Server.SignalR;

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

builder.Services.AddScoped<IAuthorizationHandler, ChangeProgressHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UploadVideoHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AddVideoHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdministratorHandler>();


builder.Services.AddAuthorization(options => {
    options.AddPolicy("Admin", policy =>
        policy.Requirements.Add(new AdministratorRequirement(true)));
    options.AddPolicy("ChangeProgress", policy =>
        policy.Requirements.Add(new ChangeProgressRequirement(true)));
    options.AddPolicy("UploadVideo", policy =>
        policy.Requirements.Add(new UploadVideoRequirement(true)));
    options.AddPolicy("AddVideo", policy =>
        policy.Requirements.Add(new AddVideoRequirement(true)));
});

builder.Services.AddResponseCompression(opts => {
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

var app = builder.Build();

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

using var scope = app.Services.CreateScope();
GeneralDbContext context = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(context.Settings.FirstOrDefault(setting => setting.Key == SettingKey.DownloadLocation).Value),
    RequestPath = "/files"
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

app.Run();