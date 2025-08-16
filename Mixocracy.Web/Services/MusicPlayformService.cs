using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Core.Enums;
using Mixocracy.Data;

namespace Mixocracy.Web.Services.Integration;

public interface IMusicPlatformService
{
    Task<Song?> ParseSongFromUrlAsync(string url);
    Task<Song?> SearchSongAsync(string query, MusicPlatform platform);
    Task<bool> CreatePlaylistOnPlatformAsync(Guid playlistId, MusicPlatform platform, Guid userId);
    Task<bool> SyncPlaylistToPlatformAsync(Guid playlistId, MusicPlatform platform);
    Task<List<Song>> GetRecommendationsAsync(Guid playlistId, int count = 10);
    Task<bool> ValidateUrlAsync(string url);
    MusicPlatform? DetectPlatformFromUrl(string url);
}

public class MusicPlatformService : IMusicPlatformService
{
    private readonly ISpotifyService _spotifyService;
    private readonly IAppleMusicService _appleMusicService;
    private readonly IYouTubeService _youtubeService;
    private readonly MixocracyDbContext _context;
    private readonly ILogger<MusicPlatformService> _logger;

    public MusicPlatformService(
        ISpotifyService spotifyService,
        IAppleMusicService appleMusicService,
        IYouTubeService youtubeService,
        MixocracyDbContext context,
        ILogger<MusicPlatformService> logger)
    {
        _spotifyService = spotifyService;
        _appleMusicService = appleMusicService;
        _youtubeService = youtubeService;
        _context = context;
        _logger = logger;
    }

    public async Task<Song?> ParseSongFromUrlAsync(string url)
    {
        try
        {
            var platform = DetectPlatformFromUrl(url);
            if (!platform.HasValue)
                return null;

            // First check if we already have this song
            var existingSong = await FindExistingSongByUrlAsync(url, platform.Value);
            if (existingSong != null)
                return existingSong;

            // Parse song metadata from the platform
            var songData = platform.Value switch
            {
                MusicPlatform.Spotify => await _spotifyService.GetSongFromUrlAsync(url),
                MusicPlatform.AppleMusic => await _appleMusicService.GetSongFromUrlAsync(url),
                MusicPlatform.YouTube or MusicPlatform.YouTubeMusic => await _youtubeService.GetSongFromUrlAsync(url),
                _ => null
            };

            if (songData == null)
                return null;

            // Try to find existing song by metadata
            var song = await FindOrCreateSongAsync(songData);

            // Create platform mapping
            await CreatePlatformMappingAsync(song.Id, platform.Value, songData.ExternalId, url, songData.PlatformMetadata);

            return song;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing song from URL: {Url}", url);
            return null;
        }
    }

    public async Task<Song?> SearchSongAsync(string query, MusicPlatform platform)
    {
        try
        {
            var songData = platform switch
            {
                MusicPlatform.Spotify => await _spotifyService.SearchSongAsync(query),
                MusicPlatform.AppleMusic => await _appleMusicService.SearchSongAsync(query),
                MusicPlatform.YouTube => await _youtubeService.SearchSongAsync(query),
                _ => null
            };

            if (songData == null)
                return null;

            var song = await FindOrCreateSongAsync(songData);
            await CreatePlatformMappingAsync(song.Id, platform, songData.ExternalId, songData.ExternalUrl, songData.PlatformMetadata);

            return song;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching song with query: {Query} on platform: {Platform}", query, platform);
            return null;
        }
    }

    public async Task<bool> CreatePlaylistOnPlatformAsync(Guid playlistId, MusicPlatform platform, Guid userId)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Platforms)
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
                return false;

            var userPlatform = await _context.UserMusicPlatforms
                .FirstOrDefaultAsync(ump => ump.UserId == userId && ump.Platform == platform && ump.IsActive);

            if (userPlatform == null)
                return false;

            var externalPlaylistId = platform switch
            {
                MusicPlatform.Spotify => await _spotifyService.CreatePlaylistAsync(playlist.Name, playlist.Description, userPlatform.AccessToken),
                MusicPlatform.AppleMusic => await _appleMusicService.CreatePlaylistAsync(playlist.Name, playlist.Description, userPlatform.AccessToken),
                MusicPlatform.YouTube => await _youtubeService.CreatePlaylistAsync(playlist.Name, playlist.Description, userPlatform.AccessToken),
                _ => null
            };

            if (string.IsNullOrEmpty(externalPlaylistId))
                return false;

            // Update or create platform mapping
            var existingPlatform = playlist.Platforms.FirstOrDefault(pp => pp.Platform == platform);
            if (existingPlatform != null)
            {
                existingPlatform.ExternalPlaylistId = externalPlaylistId;
                existingPlatform.IsActive = true;
            }
            else
            {
                var playlistPlatform = new PlaylistPlatform
                {
                    PlaylistId = playlistId,
                    Platform = platform,
                    ExternalPlaylistId = externalPlaylistId,
                    IsActive = true
                };
                _context.PlaylistPlatforms.Add(playlistPlatform);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playlist on platform {Platform} for playlist {PlaylistId}", platform, playlistId);
            return false;
        }
    }

    public async Task<bool> SyncPlaylistToPlatformAsync(Guid playlistId, MusicPlatform platform)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Platforms)
                .Include(p => p.Songs.Where(s => s.Status == PlaylistSongStatus.Approved))
                    .ThenInclude(ps => ps.Song)
                        .ThenInclude(s => s.PlatformMappings)
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null)
                return false;

            var playlistPlatform = playlist.Platforms.FirstOrDefault(pp => pp.Platform == platform && pp.IsActive);
            if (playlistPlatform?.ExternalPlaylistId == null)
                return false;

            // Get songs that exist on this platform
            var platformSongs = playlist.Songs
                .Where(ps => ps.Song.PlatformMappings.Any(pm => pm.Platform == platform && pm.IsAvailable))
                .Select(ps => ps.Song.PlatformMappings.First(pm => pm.Platform == platform).ExternalId)
                .ToList();

            var success = platform switch
            {
                MusicPlatform.Spotify => await _spotifyService.UpdatePlaylistSongsAsync(playlistPlatform.ExternalPlaylistId, platformSongs),
                MusicPlatform.AppleMusic => await _appleMusicService.UpdatePlaylistSongsAsync(playlistPlatform.ExternalPlaylistId, platformSongs),
                MusicPlatform.YouTube => await _youtubeService.UpdatePlaylistSongsAsync(playlistPlatform.ExternalPlaylistId, platformSongs),
                _ => false
            };

            if (success)
            {
                playlistPlatform.LastSyncedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing playlist {PlaylistId} to platform {Platform}", playlistId, platform);
            return false;
        }
    }

    public async Task<List<Song>> GetRecommendationsAsync(Guid playlistId, int count = 10)
    {
        try
        {
            var playlist = await _context.Playlists
                .Include(p => p.Songs.Where(s => s.Status == PlaylistSongStatus.Approved))
                    .ThenInclude(ps => ps.Song)
                        .ThenInclude(s => s.PlatformMappings)
                .FirstOrDefaultAsync(p => p.Id == playlistId);

            if (playlist == null || !playlist.Songs.Any())
                return new List<Song>();

            // Use Spotify for recommendations (most comprehensive API)
            var spotifySongs = playlist.Songs
                .Where(ps => ps.Song.PlatformMappings.Any(pm => pm.Platform == MusicPlatform.Spotify))
                .Select(ps => ps.Song.PlatformMappings.First(pm => pm.Platform == MusicPlatform.Spotify).ExternalId)
                .Take(5) // Use up to 5 seed tracks
                .ToList();

            if (!spotifySongs.Any())
                return new List<Song>();

            var recommendations = await _spotifyService.GetRecommendationsAsync(spotifySongs, count);
            
            var songs = new List<Song>();
            foreach (var rec in recommendations)
            {
                var song = await FindOrCreateSongAsync(rec);
                songs.Add(song);
            }

            return songs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for playlist {PlaylistId}", playlistId);
            return new List<Song>();
        }
    }

    public async Task<bool> ValidateUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var platform = DetectPlatformFromUrl(url);
        if (!platform.HasValue)
            return false;

        return platform.Value switch
        {
            MusicPlatform.Spotify => await _spotifyService.ValidateUrlAsync(url),
            MusicPlatform.AppleMusic => await _appleMusicService.ValidateUrlAsync(url),
            MusicPlatform.YouTube or MusicPlatform.YouTubeMusic => await _youtubeService.ValidateUrlAsync(url),
            _ => false
        };
    }

    public MusicPlatform? DetectPlatformFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var lowerUrl = url.ToLowerInvariant();

        if (lowerUrl.Contains("spotify.com"))
            return MusicPlatform.Spotify;
        
        if (lowerUrl.Contains("music.apple.com"))
            return MusicPlatform.AppleMusic;
        
        if (lowerUrl.Contains("youtube.com") || lowerUrl.Contains("youtu.be"))
            return MusicPlatform.YouTube;
        
        if (lowerUrl.Contains("music.youtube.com"))
            return MusicPlatform.YouTubeMusic;

        return null;
    }

    private async Task<Song?> FindExistingSongByUrlAsync(string url, MusicPlatform platform)
    {
        return await _context.Songs
            .Include(s => s.PlatformMappings)
            .FirstOrDefaultAsync(s => s.PlatformMappings.Any(pm => 
                pm.Platform == platform && pm.ExternalUrl == url));
    }

    private async Task<Song> FindOrCreateSongAsync(SongData songData)
    {
        // Try to find existing song by ISRC first
        Song? existingSong = null;
        
        if (!string.IsNullOrEmpty(songData.ISRC))
        {
            existingSong = await _context.Songs
                .FirstOrDefaultAsync(s => s.ISRC == songData.ISRC);
        }

        // If not found by ISRC, try by title and artist
        if (existingSong == null)
        {
            existingSong = await _context.Songs
                .FirstOrDefaultAsync(s => 
                    s.Title.ToLower() == songData.Title.ToLower() && 
                    s.Artist.ToLower() == songData.Artist.ToLower());
        }

        if (existingSong != null)
        {
            // Update any missing metadata
            if (string.IsNullOrEmpty(existingSong.ISRC) && !string.IsNullOrEmpty(songData.ISRC))
                existingSong.ISRC = songData.ISRC;
            
            if (string.IsNullOrEmpty(existingSong.Album) && !string.IsNullOrEmpty(songData.Album))
                existingSong.Album = songData.Album;
                
            if (!existingSong.DurationMs.HasValue && songData.DurationMs.HasValue)
                existingSong.DurationMs = songData.DurationMs;
                
            if (string.IsNullOrEmpty(existingSong.CoverImageUrl) && !string.IsNullOrEmpty(songData.CoverImageUrl))
                existingSong.CoverImageUrl = songData.CoverImageUrl;

            await _context.SaveChangesAsync();
            return existingSong;
        }

        // Create new song
        var newSong = new Song
        {
            Title = songData.Title,
            Artist = songData.Artist,
            Album = songData.Album,
            DurationMs = songData.DurationMs,
            ReleaseYear = songData.ReleaseYear,
            CoverImageUrl = songData.CoverImageUrl,
            ISRC = songData.ISRC,
            Metadata = songData.Metadata != null ? System.Text.Json.JsonSerializer.Serialize(songData.Metadata) : null
        };

        _context.Songs.Add(newSong);
        await _context.SaveChangesAsync();
        return newSong;
    }

    private async Task CreatePlatformMappingAsync(Guid songId, MusicPlatform platform, string externalId, string? externalUrl, object? metadata)
    {
        var existingMapping = await _context.SongPlatformMappings
            .FirstOrDefaultAsync(spm => spm.SongId == songId && spm.Platform == platform);

        if (existingMapping == null)
        {
            var mapping = new SongPlatformMapping
            {
                SongId = songId,
                Platform = platform,
                ExternalId = externalId,
                ExternalUrl = externalUrl,
                PlatformMetadata = metadata != null ? System.Text.Json.JsonSerializer.Serialize(metadata) : null
            };

            _context.SongPlatformMappings.Add(mapping);
            await _context.SaveChangesAsync();
        }
    }
}

// Helper class for transferring song data between services
public class SongData
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string? Album { get; set; }
    public int? DurationMs { get; set; }
    public int? ReleaseYear { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ISRC { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalUrl { get; set; }
    public object? Metadata { get; set; }
    public object? PlatformMetadata { get; set; }
}