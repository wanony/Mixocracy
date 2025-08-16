using System.ComponentModel.DataAnnotations;
using Mixocracy.Core.Enums;

namespace Mixocracy.Core.Models;

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }
    
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Settings
    public bool IsPublic { get; set; } = false;
    public double VotingThreshold { get; set; } = 0.5; // >50%
    public bool RequireVotingForAddition { get; set; } = true;
    public bool RequireVotingForRemoval { get; set; } = true;
    
    // Invite system
    public string InviteCode { get; set; } = GenerateInviteCode();
    public DateTime? InviteExpiresAt { get; set; }
    
    // Navigation properties
    public virtual User CreatedBy { get; set; } = null!;
    public virtual ICollection<PlaylistMember> Members { get; set; } = new List<PlaylistMember>();
    public virtual ICollection<PlaylistSong> Songs { get; set; } = new List<PlaylistSong>();
    public virtual ICollection<PlaylistActivity> Activities { get; set; } = new List<PlaylistActivity>();
    public virtual ICollection<PlaylistPlatform> Platforms { get; set; } = new List<PlaylistPlatform>();
    
    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class PlaylistMember
{
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }
    public PlaylistRole Role { get; set; } = PlaylistRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class PlaylistSong
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistId { get; set; }
    public Guid SongId { get; set; }
    public Guid AddedByUserId { get; set; }
    
    public PlaylistSongStatus Status { get; set; } = PlaylistSongStatus.Pending;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    
    public int Position { get; set; } = 0; // For ordering
    
    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
    public virtual Song Song { get; set; } = null!;
    public virtual User AddedBy { get; set; } = null!;
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

public class PlaylistPlatform
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistId { get; set; }
    public MusicPlatform Platform { get; set; }
    
    [MaxLength(500)]
    public string? ExternalPlaylistId { get; set; }
    
    [MaxLength(1000)]
    public string? ExternalPlaylistUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
}