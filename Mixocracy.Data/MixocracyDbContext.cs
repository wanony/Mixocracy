using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;

namespace Mixocracy.Data;

public class MixocracyDbContext : DbContext
{
    public MixocracyDbContext(DbContextOptions<MixocracyDbContext> options) : base(options)
    {
    }

    // DbSets
    public DbSet<User> Users { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistMember> PlaylistMembers { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<SongPlatformMapping> SongPlatformMappings { get; set; }
    public DbSet<PlaylistSong> PlaylistSongs { get; set; }
    public DbSet<PlaylistPlatform> PlaylistPlatforms { get; set; }
    public DbSet<UserMusicPlatform> UserMusicPlatforms { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<PlaylistActivity> PlaylistActivities { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configurations
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Playlist configurations
        modelBuilder.Entity<Playlist>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InviteCode).IsUnique();
            
            entity.HasOne(e => e.CreatedBy)
                .WithMany(e => e.CreatedPlaylists)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PlaylistMember configurations
        modelBuilder.Entity<PlaylistMember>(entity =>
        {
            entity.HasKey(e => new { e.PlaylistId, e.UserId });
            
            entity.HasOne(e => e.Playlist)
                .WithMany(e => e.Members)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany(e => e.PlaylistMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Song configurations
        modelBuilder.Entity<Song>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ISRC);
            entity.HasIndex(e => new { e.Title, e.Artist });
            
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // SongPlatformMapping configurations
        modelBuilder.Entity<SongPlatformMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Platform, e.ExternalId }).IsUnique();
            
            entity.HasOne(e => e.Song)
                .WithMany(e => e.PlatformMappings)
                .HasForeignKey(e => e.SongId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PlaylistSong configurations
        modelBuilder.Entity<PlaylistSong>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PlaylistId, e.SongId, e.Status });
            
            entity.HasOne(e => e.Playlist)
                .WithMany(e => e.Songs)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Song)
                .WithMany(e => e.PlaylistSongs)
                .HasForeignKey(e => e.SongId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.AddedBy)
                .WithMany()
                .HasForeignKey(e => e.AddedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.Property(e => e.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PlaylistPlatform configurations
        modelBuilder.Entity<PlaylistPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PlaylistId, e.Platform }).IsUnique();
            
            entity.HasOne(e => e.Playlist)
                .WithMany(e => e.Platforms)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // UserMusicPlatform configurations
        modelBuilder.Entity<UserMusicPlatform>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Platform }).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.MusicPlatforms)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.ConnectedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Vote configurations
        modelBuilder.Entity<Vote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.PlaylistSongId }).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.Votes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.PlaylistSong)
                .WithMany(e => e.Votes)
                .HasForeignKey(e => e.PlaylistSongId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // PlaylistActivity configurations
        modelBuilder.Entity<PlaylistActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PlaylistId, e.CreatedAt });
            
            entity.HasOne(e => e.Playlist)
                .WithMany(e => e.Activities)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.User)
                .WithMany(e => e.Activities)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(e => e.PlaylistSong)
                .WithMany()
                .HasForeignKey(e => e.PlaylistSongId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Notification configurations
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt });
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Playlist)
                .WithMany()
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(e => e.PlaylistSong)
                .WithMany()
                .HasForeignKey(e => e.PlaylistSongId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}