using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Core.Enums;
using Mixocracy.Data;

namespace Mixocracy.Web.Services;

public interface IVotingService
{
    Task<bool> CastVoteAsync(Guid userId, Guid playlistSongId, VoteType voteType, VoteAction action);
    Task<(int upvotes, int downvotes)> GetVoteCountsAsync(Guid playlistSongId);
    Task<VoteType?> GetUserVoteAsync(Guid userId, Guid playlistSongId);
    Task<bool> ProcessVotingResultAsync(Guid playlistSongId);
    Task<bool> ShouldBypassVotingAsync(Guid playlistId);
}

public class VotingService : IVotingService
{
    private readonly MixocracyDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<VotingService> _logger;

    public VotingService(
        MixocracyDbContext context, 
        INotificationService notificationService,
        ILogger<VotingService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<bool> CastVoteAsync(Guid userId, Guid playlistSongId, VoteType voteType, VoteAction action)
    {
        try
        {
            var playlistSong = await _context.PlaylistSongs
                .Include(ps => ps.Playlist)
                .ThenInclude(p => p.Members)
                .Include(ps => ps.Song)
                .FirstOrDefaultAsync(ps => ps.Id == playlistSongId);

            if (playlistSong == null)
                return false;

            // Check if user is a member of the playlist
            var isMember = playlistSong.Playlist.Members.Any(m => m.UserId == userId && m.IsActive);
            if (!isMember)
                return false;

            // Check if voting is bypassed for small groups
            if (await ShouldBypassVotingAsync(playlistSong.PlaylistId))
            {
                // Auto-approve for small groups
                if (action == VoteAction.Add)
                {
                    playlistSong.Status = PlaylistSongStatus.Approved;
                    playlistSong.ApprovedAt = DateTime.UtcNow;
                }
                else
                {
                    playlistSong.Status = PlaylistSongStatus.Removed;
                    playlistSong.RemovedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await LogActivityAsync(playlistSong.PlaylistId, userId, playlistSongId, 
                    action == VoteAction.Add ? ActivityType.SongApproved : ActivityType.SongRemoved);
                
                return true;
            }

            // Handle voting
            var existingVote = await _context.Votes
                .FirstOrDefaultAsync(v => v.UserId == userId && v.PlaylistSongId == playlistSongId);

            if (existingVote != null)
            {
                // Update existing vote
                existingVote.Type = voteType;
                existingVote.Action = action;
                existingVote.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new vote
                var vote = new Vote
                {
                    UserId = userId,
                    PlaylistSongId = playlistSongId,
                    Type = voteType,
                    Action = action
                };
                _context.Votes.Add(vote);
            }

            await _context.SaveChangesAsync();

            // Log voting activity
            await LogActivityAsync(playlistSong.PlaylistId, userId, playlistSongId, ActivityType.VoteCast);

            // Check if voting threshold is met
            await ProcessVotingResultAsync(playlistSongId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error casting vote for user {UserId} on playlist song {PlaylistSongId}", userId, playlistSongId);
            return false;
        }
    }

    public async Task<(int upvotes, int downvotes)> GetVoteCountsAsync(Guid playlistSongId)
    {
        var votes = await _context.Votes
            .Where(v => v.PlaylistSongId == playlistSongId)
            .ToListAsync();

        var upvotes = votes.Count(v => v.Type == VoteType.Upvote);
        var downvotes = votes.Count(v => v.Type == VoteType.Downvote);

        return (upvotes, downvotes);
    }

    public async Task<VoteType?> GetUserVoteAsync(Guid userId, Guid playlistSongId)
    {
        var vote = await _context.Votes
            .FirstOrDefaultAsync(v => v.UserId == userId && v.PlaylistSongId == playlistSongId);

        return vote?.Type;
    }

    public async Task<bool> ProcessVotingResultAsync(Guid playlistSongId)
    {
        try
        {
            var playlistSong = await _context.PlaylistSongs
                .Include(ps => ps.Playlist)
                .ThenInclude(p => p.Members.Where(m => m.IsActive))
                .Include(ps => ps.Votes)
                .Include(ps => ps.Song)
                .FirstOrDefaultAsync(ps => ps.Id == playlistSongId);

            if (playlistSong == null || playlistSong.Status != PlaylistSongStatus.Pending)
                return false;

            var totalMembers = playlistSong.Playlist.Members.Count;
            var threshold = playlistSong.Playlist.VotingThreshold;
            var requiredVotes = Math.Ceiling(totalMembers * threshold);

            var (upvotes, downvotes) = await GetVoteCountsAsync(playlistSongId);
            var totalVotes = upvotes + downvotes;

            // Check if enough people have voted
            if (totalVotes >= requiredVotes)
            {
                if (upvotes > downvotes)
                {
                    playlistSong.Status = PlaylistSongStatus.Approved;
                    playlistSong.ApprovedAt = DateTime.UtcNow;
                    
                    await LogActivityAsync(playlistSong.PlaylistId, playlistSong.AddedByUserId, 
                        playlistSongId, ActivityType.SongApproved);
                    
                    // Notify all members
                    await _notificationService.NotifyPlaylistMembersAsync(
                        playlistSong.PlaylistId,
                        NotificationType.SongApproved,
                        "Song Approved",
                        $"'{playlistSong.Song.Title}' by {playlistSong.Song.Artist} has been approved!");
                }
                else
                {
                    playlistSong.Status = PlaylistSongStatus.Rejected;
                    
                    await LogActivityAsync(playlistSong.PlaylistId, playlistSong.AddedByUserId, 
                        playlistSongId, ActivityType.SongRejected);
                    
                    // Notify all members
                    await _notificationService.NotifyPlaylistMembersAsync(
                        playlistSong.PlaylistId,
                        NotificationType.SongRejected,
                        "Song Rejected",
                        $"'{playlistSong.Song.Title}' by {playlistSong.Song.Artist} was not approved.");
                }

                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voting result for playlist song {PlaylistSongId}", playlistSongId);
            return false;
        }
    }

    public async Task<bool> ShouldBypassVotingAsync(Guid playlistId)
    {
        var memberCount = await _context.PlaylistMembers
            .CountAsync(pm => pm.PlaylistId == playlistId && pm.IsActive);
        
        return memberCount <= 2;
    }

    private async Task LogActivityAsync(Guid playlistId, Guid userId, Guid? playlistSongId, ActivityType activityType)
    {
        var activity = new PlaylistActivity
        {
            PlaylistId = playlistId,
            UserId = userId,
            PlaylistSongId = playlistSongId,
            Type = activityType,
            Description = GetActivityDescription(activityType)
        };

        _context.PlaylistActivities.Add(activity);
        await _context.SaveChangesAsync();
    }

    private static string GetActivityDescription(ActivityType activityType)
    {
        return activityType switch
        {
            ActivityType.VoteCast => "Cast a vote",
            ActivityType.SongApproved => "Song was approved by vote",
            ActivityType.SongRejected => "Song was rejected by vote",
            ActivityType.SongRemoved => "Song was removed",
            _ => activityType.ToString()
        };
    }
}