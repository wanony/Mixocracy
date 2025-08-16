using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Core.Enums;
using Mixocracy.Data;

namespace Mixocracy.Web.Services;

public interface INotificationService
{
    Task<bool> CreateNotificationAsync(Guid userId, NotificationType type, string title, string? message = null, 
        Guid? playlistId = null, Guid? playlistSongId = null, string? actionUrl = null, object? metadata = null);
    Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int limit = 50);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);
    Task<bool> MarkAllAsReadAsync(Guid userId);
    Task<bool> NotifyPlaylistMembersAsync(Guid playlistId, NotificationType type, string title, string message, 
        Guid? excludeUserId = null, Guid? playlistSongId = null);
    Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId);
}

public class NotificationService : INotificationService
{
    private readonly MixocracyDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(MixocracyDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> CreateNotificationAsync(Guid userId, NotificationType type, string title, string? message = null,
        Guid? playlistId = null, Guid? playlistSongId = null, string? actionUrl = null, object? metadata = null)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                PlaylistId = playlistId,
                PlaylistSongId = playlistSongId,
                ActionUrl = actionUrl,
                Metadata = metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : null
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification for user {UserId}", userId);
            return false;
        }
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId, bool unreadOnly = false, int limit = 50)
    {
        var query = _context.Notifications
            .Include(n => n.Playlist)
            .Include(n => n.PlaylistSong)
                .ThenInclude(ps => ps!.Song)
            .Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", notificationId, userId);
            return false;
        }
    }

    public async Task<bool> MarkAllAsReadAsync(Guid userId)
    {
        try
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> NotifyPlaylistMembersAsync(Guid playlistId, NotificationType type, string title, string message,
        Guid? excludeUserId = null, Guid? playlistSongId = null)
    {
        try
        {
            var memberIds = await _context.PlaylistMembers
                .Where(pm => pm.PlaylistId == playlistId && pm.IsActive)
                .Select(pm => pm.UserId)
                .ToListAsync();

            if (excludeUserId.HasValue)
            {
                memberIds = memberIds.Where(id => id != excludeUserId.Value).ToList();
            }

            var actionUrl = GenerateActionUrl(type, playlistId, playlistSongId);

            var notifications = memberIds.Select(userId => new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                PlaylistId = playlistId,
                PlaylistSongId = playlistSongId,
                ActionUrl = actionUrl
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying playlist members for playlist {PlaylistId}", playlistId);
            return false;
        }
    }

    public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId} for user {UserId}", notificationId, userId);
            return false;
        }
    }

    private static string? GenerateActionUrl(NotificationType type, Guid playlistId, Guid? playlistSongId = null)
    {
        return type switch
        {
            NotificationType.PlaylistInvite => $"/playlist/{playlistId}",
            NotificationType.VoteRequest => $"/playlist/{playlistId}?highlight={playlistSongId}",
            NotificationType.SongApproved or NotificationType.SongRejected or NotificationType.SongRemoved => $"/playlist/{playlistId}",
            NotificationType.PlaylistUpdated => $"/playlist/{playlistId}",
            NotificationType.MemberJoined or NotificationType.MemberLeft => $"/playlist/{playlistId}/members",
            _ => $"/playlist/{playlistId}"
        };
    }
}