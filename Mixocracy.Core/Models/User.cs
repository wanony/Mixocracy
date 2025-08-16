using System.ComponentModel.DataAnnotations;

namespace Mixocracy.Core.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(255)]
    public string? DisplayName { get; set; }
    
    [MaxLength(500)]
    public string? ProfileImageUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<PlaylistMember> PlaylistMemberships { get; set; } = new List<PlaylistMember>();
    public virtual ICollection<Playlist> CreatedPlaylists { get; set; } = new List<Playlist>();
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public virtual ICollection<UserMusicPlatform> MusicPlatforms { get; set; } = new List<UserMusicPlatform>();
    public virtual ICollection<PlaylistActivity> Activities { get; set; } = new List<PlaylistActivity>();
}