using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mixocracy.Core.Enums;

namespace Mixocracy.Core.Models;

public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlaylistSongId { get; set; }
    
    public VoteType Type { get; set; }
    public VoteAction Action { get; set; } = VoteAction.Add; // Add or Remove
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual PlaylistSong PlaylistSong { get; set; } = null!;
}

public class PlaylistActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaylistId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PlaylistSongId { get; set; }
    
    public ActivityType Type { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    // Additional context as JSON
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Playlist Playlist { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual PlaylistSong? PlaylistSong { get; set; }
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? PlaylistId { get; set; }
    public Guid? PlaylistSongId { get; set; }
    
    public NotificationType Type { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Message { get; set; }
    
    [MaxLength(1000)]
    public string? ActionUrl { get; set; }
    
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    
    // Additional context as JSON
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Playlist? Playlist { get; set; }
    public virtual PlaylistSong? PlaylistSong { get; set; }
}