using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Data;

namespace Mixocracy.Web.Services;

public interface IUserService
{
    Task<User?> CreateUserAsync(string username, string email, string? displayName = null);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> UpdateUserAsync(Guid userId, string? displayName, string? profileImageUrl);
    Task<List<User>> SearchUsersAsync(string query, int limit = 10);
    Task<bool> IsUsernameAvailableAsync(string username);
    Task<bool> IsEmailAvailableAsync(string email);
}

public class UserService : IUserService
{
    private readonly MixocracyDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(MixocracyDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<User?> CreateUserAsync(string username, string email, string? displayName = null)
    {
        try
        {
            // Check if username or email already exists
            if (!await IsUsernameAvailableAsync(username) || !await IsEmailAvailableAsync(email))
            {
                return null;
            }

            var user = new User
            {
                Username = username,
                Email = email,
                DisplayName = displayName ?? username
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with username {Username} and email {Email}", username, email);
            return null;
        }
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> UpdateUserAsync(Guid userId, string? displayName, string? profileImageUrl)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return false;

            if (!string.IsNullOrWhiteSpace(displayName))
                user.DisplayName = displayName;

            if (!string.IsNullOrWhiteSpace(profileImageUrl))
                user.ProfileImageUrl = profileImageUrl;

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return false;
        }
    }

    public async Task<List<User>> SearchUsersAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<User>();

        var lowerQuery = query.ToLower();

        return await _context.Users
            .Where(u => u.Username.ToLower().Contains(lowerQuery) || 
                       u.DisplayName!.ToLower().Contains(lowerQuery) ||
                       u.Email.ToLower().Contains(lowerQuery))
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        return !await _context.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower());
    }

    public async Task<bool> IsEmailAvailableAsync(string email)
    {
        return !await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }
}