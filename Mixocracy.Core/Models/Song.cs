using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Mixocracy.Core.Enums;

namespace Mixocracy.Core.Models;

public class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(300)]
    public string Artist { get; set; } = string.Empty;
    
    [MaxLength(300)]
    public string? Album { get; set; }
    
    public int? DurationMs { get; set; }
    public int? ReleaseYear { get; set; }
    
    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }
    
    // ISRC code for cross-platform matching
    [MaxLength(20)]
    public string? ISRC { get; set; }
    
    // Additional metadata as JSON
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<SongPlatformMapping> PlatformMappings { get; set; } = new List<SongPlatformMapping>();
    public virtual ICollection<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
}

public class SongPlatformMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SongId { get; set; }
    public MusicPlatform Platform { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string ExternalId { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }
    
    // Platform-specific metadata
    [Column(TypeName = "jsonb")]
    public string? PlatformMetadata { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsAvailable { get; set; } = true;
    
    // Navigation properties
    public virtual Song Song { get; set; } = null!;
}

public class UserMusicPlatform
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public MusicPlatform Platform { get; set; }
    
    [MaxLength(500)]
    public string? ExternalUserId { get; set; }
    
    [MaxLength(200)]
    public string? DisplayName { get; set; }
    
    // Encrypted tokens
    [MaxLength(2000)]
    public string? AccessToken { get; set; }
    
    [MaxLength(2000)]
    public string? RefreshToken { get; set; }
    
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}