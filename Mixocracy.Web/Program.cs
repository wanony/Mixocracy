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

// Music Platform Services
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

// Ensure database is created and seeded (for development)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MixocracyDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Ensure database exists
        if (context.Database.IsInMemory())
        {
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("Using in-memory database");
        }
        else
        {
            // For real databases, use migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying pending migrations...");
                await context.Database.MigrateAsync();
            }
        }

        // Seed test data in development environment
        if (app.Environment.IsDevelopment())
        {
            logger.LogInformation("Seeding development test data...");
            await DevDataSeeder.SeedTestDataAsync(context);
            logger.LogInformation("Test data seeded successfully!");
            
            // Log test users for easy access
            logger.LogInformation("=== TEST USERS AVAILABLE ===");
            logger.LogInformation("Username: testuser, Email: test@example.com, Display: Test User");
            logger.LogInformation("Username: alice, Email: alice@example.com, Display: Alice Johnson");
            logger.LogInformation("Username: bob, Email: bob@example.com, Display: Bob Smith");
            logger.LogInformation("Username: charlie, Email: charlie@example.com, Display: Charlie Brown");
            logger.LogInformation("Username: diana, Email: diana@example.com, Display: Diana Prince");
            logger.LogInformation("=============================");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while creating/seeding the database");
        throw;
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