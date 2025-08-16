using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Core.Enums;
using Mixocracy.Data;
using Mixocracy.Web.Services.Integration;

namespace Mixocracy.Web.Services;

public interface IPlaylistService
{
    Task<Playlist?> CreatePlaylistAsync(Guid userId, string name, string? description, List<MusicPlatform> platforms);
    Task<Playlist?> GetPlaylistAsync(Guid playlistId, Guid? userId = null);
    Task<List<Playlist>> GetUserPlaylistsAsync(Guid userId);
    Task<bool> JoinPlaylistAsync(string inviteCode, Guid userId);
    Task<bool> AddSongToPlaylistAsync(Guid playlistId, Guid userId, string songUrl);
    Task<bool> RemoveSongFromPlaylistAsync(Guid playlistSongId, Guid userId);
    Task<List<PlaylistSong>> GetPlaylistSongsAsync(Guid playlistId, PlaylistSongStatus? status = null);
    Task<List<PlaylistActivity>> GetPlaylistActivityAsync(Guid playlistId, int limit = 50);
    Task<bool> UpdatePlaylistAsync(Guid playlistId, Guid userId, string? name, string? description);
    Task<string?> RefreshInviteCodeAsync(Guid playlistId, Guid userId);
}

public class PlaylistService : IPlaylistService
{
    private readonly MixocracyDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IMusicPlatformService _musicPlatformService;
    private readonly ILogger<PlaylistService> _logger;

    public PlaylistService(
        MixocracyDbContext context,
        INotificationService notificationService,
        IMusicPlatformService musicPlatformService,
        ILogger<PlaylistService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _musicPlatformService = musicPlatformService;
        _logger = logger;
    }

    public async Task<Playlist?> CreatePlaylistAsync(Guid userId, string name, string? description, List<MusicPlatform> platforms)
    {
        try
        {
            var playlist = new Playlist
            {
                Name = name,
                Description = description,
                CreatedByUserId = userId
            };

            _context.Playlists.Add(playlist);

            // Add creator as owner
            var ownerMembership = new PlaylistMember
            {
                PlaylistId = playlist.Id,
                UserId = userId,
                Role = PlaylistRole.Owner
            };
            _context.PlaylistMembers.Add(ownerMembership);

            // Add selected platforms
            foreach (var platform in platforms)
            {
                var playlistPlatform = new PlaylistPlatform
                {
                    PlaylistId = playlist.Id,
                    Platform = platform
                };
                _context.PlaylistPlatforms.Add(playlistPlatform);
            }

            // Log activity
            var activity = new PlaylistActivity
            {
                PlaylistId = playlist.Id,
                UserId = userId,
                Type = ActivityType.PlaylistCreated,
                Description = "Playlist created"
            };
            _context.PlaylistActivities.Add(activity);

            await _context.SaveChangesAsync();

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist for user {UserId}", userId);
            return null;
        }
    }

    public async Task<Playlist?> GetPlaylistAsync(Guid playlistId, Guid? userId = null)
    {
        var query = _context.Playlists
            .Include(p => p.CreatedBy)
            .Include(p => p.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Include(p => p.Platforms)
            .AsQueryable();

        if (userId.HasValue)
        {
            // Only return if user is a member or playlist is public
            query = query.Where(p => p.Id == playlistId && 
                (p.IsPublic || p.Members.Any(m => m.UserId == userId.Value && m.IsActive)));
        }
        else
        {
            query = query.Where(p => p.Id == playlistId && p.IsPublic);
        }

        return await query.FirstOrDefaultAsync();
    }

    public async Task<List<Playlist>> GetUserPlaylistsAsync(Guid userId)
    {
        return await _context.Playlists
            .Include(p => p.CreatedBy)
            .Include(p => p.Members.Where(m => m.IsActive))
                .ThenInclude(m => m.User)
            .Where(p => p.Members.Any(m => m.UserId == userId && m.IsActive))
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    public async Task<bool> JoinPlaylistAsync(string inviteCode, Guid userId)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.InviteCode == inviteCode && 
                    (p.InviteExpiresAt == null || p.InviteExpiresAt > DateTime.UtcNow));

            if (playlist == null)
                return false;

            // Check if user is already a member
            var existingMembership = playlist.Members.FirstOrDefault(m => m.UserId == userId);
            if (existingMembership != null)
            {
                if (!existingMembership.IsActive)
                {
                    existingMembership.IsActive = true;
                    existingMembership.JoinedAt = DateTime.UtcNow;
                }
                else
                {
                    return false; // Already an active member
                }
            }
            else
            {
                var membership = new PlaylistMember
                {
                    PlaylistId = playlist.Id,
                    UserId = userId,
                    Role = PlaylistRole.Member
                };
                _context.PlaylistMembers.Add(membership);
            }

            // Log activity
            var activity = new PlaylistActivity
            {
                PlaylistId = playlist.Id,
                UserId = userId,
                Type = ActivityType.MemberJoined,
                Description = "Joined the playlist"
            };
            _context.PlaylistActivities.Add(activity);

            await _context.SaveChangesAsync();

            // Notify other members
            await _notificationService.NotifyPlaylistMembersAsync(
                playlist.Id,
                NotificationType.MemberJoined,
                "New Member",
                "A new member has joined the playlist!",
                excludeUserId: userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining playlist with invite code {InviteCode} for user {UserId}", inviteCode, userId);
            return false;
        }
    }

    public async Task<bool> AddSongToPlaylistAsync(Guid playlistId, Guid userId, string songUrl)
    {
        try
        {
            // Check if user is a member
            var isMember = await _context.PlaylistMembers
                .AnyAsync(pm => pm.PlaylistId == playlistId && pm.UserId == userId && pm.IsActive);

            if (!isMember)
                return false;

            // Parse song from URL and get/create song record
            var song = await _musicPlatformService.ParseSongFromUrlAsync(songUrl);
            if (song == null)
                return false;

            // Check if song is already in playlist
            var existingSong = await _context.PlaylistSongs
                .AnyAsync(ps => ps.PlaylistId == playlistId && ps.SongId == song.Id && 
                    ps.Status != PlaylistSongStatus.Rejected && ps.Status != PlaylistSongStatus.Removed);

            if (existingSong)
                return false;

            var playlistSong = new PlaylistSong
            {
                PlaylistId = playlistId,
                SongId = song.Id,
                AddedByUserId = userId,
                Status = PlaylistSongStatus.Pending
            };

            _context.PlaylistSongs.Add(playlistSong);

            // Log activity
            var activity = new PlaylistActivity
            {
                PlaylistId = playlistId,
                UserId = userId,
                PlaylistSongId = playlistSong.Id,
                Type = ActivityType.SongAdded,
                Description = $"Added '{song.Title}' by {song.Artist}"
            };
            _context.PlaylistActivities.Add(activity);

            await _context.SaveChangesAsync();

            // Notify other members to vote
            await _notificationService.NotifyPlaylistMembersAsync(
                playlistId,
                NotificationType.VoteRequest,
                "Vote Required",
                $"'{song.Title}' by {song.Artist} needs your vote!",
                excludeUserId: userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding song to playlist {PlaylistId} for user {UserId}", playlistId, userId);
            return false;
        }
    }

    public async Task<bool> RemoveSongFromPlaylistAsync(Guid playlistSongId, Guid userId)
    {
        try
        {
            var playlistSong = await _context.PlaylistSongs
                .Include(ps => ps.Playlist)
                .Include(ps => ps.Song)
                .FirstOrDefaultAsync(ps => ps.Id == playlistSongId);

            if (playlistSong == null)
                return false;

            // Check if user is a member
            var isMember = await _context.PlaylistMembers
                .AnyAsync(pm => pm.PlaylistId == playlistSong.PlaylistId && pm.UserId == userId && pm.IsActive);

            if (!isMember)
                return false;

            // Mark as removed
            playlistSong.Status = PlaylistSongStatus.Removed;
            playlistSong.RemovedAt = DateTime.UtcNow;

            // Log activity
            var activity = new PlaylistActivity
            {
                PlaylistId = playlistSong.PlaylistId,
                UserId = userId,
                PlaylistSongId = playlistSongId,
                Type = ActivityType.SongRemoved,
                Description = $"Removed '{playlistSong.Song.Title}' by {playlistSong.Song.Artist}"
            };
            _context.PlaylistActivities.Add(activity);

            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing song {PlaylistSongId} for user {UserId}", playlistSongId, userId);
            return false;
        }
    }

    public async Task<List<PlaylistSong>> GetPlaylistSongsAsync(Guid playlistId, PlaylistSongStatus? status = null)
    {
        var query = _context.PlaylistSongs
            .Include(ps => ps.Song)
                .ThenInclude(s => s.PlatformMappings)
            .Include(ps => ps.AddedBy)
            .Include(ps => ps.Votes)
                .ThenInclude(v => v.User)
            .Where(ps => ps.PlaylistId == playlistId);

        if (status.HasValue)
        {
            query = query.Where(ps => ps.Status == status.Value);
        }

        return await query
            .OrderBy(ps => ps.Position)
            .ThenBy(ps => ps.AddedAt)
            .ToListAsync();
    }

    public async Task<List<PlaylistActivity>> GetPlaylistActivityAsync(Guid playlistId, int limit = 50)
    {
        return await _context.PlaylistActivities
            .Include(pa => pa.User)
            .Include(pa => pa.PlaylistSong)
                .ThenInclude(ps => ps!.Song)
            .Where(pa => pa.PlaylistId == playlistId)
            .OrderByDescending(pa => pa.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> UpdatePlaylistAsync(Guid playlistId, Guid userId, string? name, string? description)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
                return false;

            // Check if user has permission to update
            var member = playlist.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
            if (member == null || (member.Role != PlaylistRole.Owner && member.Role != PlaylistRole.Admin))
                return false;

            if (!string.IsNullOrWhiteSpace(name))
                playlist.Name = name;

            if (description != null)
                playlist.Description = description;

            playlist.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating playlist {PlaylistId} for user {UserId}", playlistId, userId);
            return false;
        }
    }

    public async Task<string?> RefreshInviteCodeAsync(Guid playlistId, Guid userId)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
                return null;

            // Check if user has permission
            var member = playlist.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
            if (member == null || (member.Role != PlaylistRole.Owner && member.Role != PlaylistRole.Admin))
                return null;

            playlist.InviteCode = GenerateInviteCode();
            await _context.SaveChangesAsync();

            return playlist.InviteCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing invite code for playlist {PlaylistId}", playlistId);
            return null;
        }
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}