using Microsoft.EntityFrameworkCore;
using Mixocracy.Web.Components;
using Mixocracy.Data;
using Mixocracy.Web.Services;
using Mixocracy.Web.Services.Integration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database - Use in-memory for development if no connection string provided
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<MixocracyDbContext>(options =>
        options.UseInMemoryDatabase("MixocracyDb"));
}
else
{
    builder.Services.AddDbContext<MixocracyDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Application Services
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddScoped<IVotingService, VotingService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserService, UserService>();

// Music Platform Services (commented out for now)
builder.Services.AddScoped<IMusicPlatformService, MusicPlatformService>();
builder.Services.AddScoped<ISpotifyService, SpotifyService>();
builder.Services.AddScoped<IAppleMusicService, AppleMusicService>();
builder.Services.AddScoped<IYouTubeService, YouTubeService>();

// Authentication (simplified for development)
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
    });

// SignalR for real-time updates
builder.Services.AddSignalR();

// HTTP Client for external API calls
builder.Services.AddHttpClient();

// Logging
builder.Services.AddLogging();

var app = builder.Build();

// Ensure database is created (for development)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MixocracyDbContext>();
    if (context.Database.IsInMemory())
    {
        context.Database.EnsureCreated();
        
        // Seed some test data
        if (!context.Users.Any())
        {
            var testUser = new Mixocracy.Core.Models.User
            {
                Username = "testuser",
                Email = "test@example.com",
                DisplayName = "Test User"
            };
            context.Users.Add(testUser);
            context.SaveChanges();
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SignalR Hub for real-time notifications
app.MapHub<NotificationHub>("/notificationhub");

app.Run();