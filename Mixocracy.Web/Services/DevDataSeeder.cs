using Microsoft.EntityFrameworkCore;
using Mixocracy.Core.Models;
using Mixocracy.Core.Enums;
using Mixocracy.Data;

namespace Mixocracy.Web.Services;

public static class DevDataSeeder
{
    public static async Task SeedTestDataAsync(MixocracyDbContext context)
    {
        // Don't seed if data already exists
        if (await context.Users.AnyAsync())
            return;

        await SeedUsersAsync(context);
        await SeedPlaylistsAsync(context);
        await SeedSongsAsync(context);
        await SeedActivitiesAsync(context);
        
        await context.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(MixocracyDbContext context)
    {
        var users = new List<User>
        {
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Username = "testuser",
                Email = "test@example.com",
                DisplayName = "Test User",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Username = "alice",
                Email = "alice@example.com",
                DisplayName = "Alice Johnson",
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },
            new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Username = "bob",
                Email = "bob@example.com",
                DisplayName = "Bob Smith",
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new User
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Username = "charlie",
                Email = "charlie@example.com",
                DisplayName = "Charlie Brown",
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            },
            new User
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Username = "diana",
                Email = "diana@example.com",
                DisplayName = "Diana Prince",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private static async Task SeedPlaylistsAsync(MixocracyDbContext context)
    {
        var testUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var aliceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var bobId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var playlists = new List<Playlist>
        {
            new Playlist
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Friday Night Vibes",
                Description = "The perfect playlist for Friday night hangouts",
                CreatedByUserId = testUserId,
                IsPublic = true,
                VotingThreshold = 0.6,
                InviteCode = "FRIDAY01",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Playlist
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = "Road Trip Mix",
                Description = "Songs for our upcoming road trip adventure",
                CreatedByUserId = aliceId,
                IsPublic = false,
                VotingThreshold = 0.5,
                InviteCode = "ROADTRIP",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Playlist
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Workout Beats",
                Description = "High-energy songs to power through workouts",
                CreatedByUserId = bobId,
                IsPublic = true,
                VotingThreshold = 0.7,
                InviteCode = "WORKOUT1",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        context.Playlists.AddRange(playlists);
        await context.SaveChangesAsync();

        // Add playlist members
        var members = new List<PlaylistMember>
        {
            // Friday Night Vibes members
            new PlaylistMember { PlaylistId = playlists[0].Id, UserId = testUserId, Role = PlaylistRole.Owner },
            new PlaylistMember { PlaylistId = playlists[0].Id, UserId = aliceId, Role = PlaylistRole.Member },
            new PlaylistMember { PlaylistId = playlists[0].Id, UserId = bobId, Role = PlaylistRole.Member },
            
            // Road Trip Mix members
            new PlaylistMember { PlaylistId = playlists[1].Id, UserId = aliceId, Role = PlaylistRole.Owner },
            new PlaylistMember { PlaylistId = playlists[1].Id, UserId = testUserId, Role = PlaylistRole.Admin },
            
            // Workout Beats members
            new PlaylistMember { PlaylistId = playlists[2].Id, UserId = bobId, Role = PlaylistRole.Owner },
            new PlaylistMember { PlaylistId = playlists[2].Id, UserId = testUserId, Role = PlaylistRole.Member }
        };

        context.PlaylistMembers.AddRange(members);

        // Add playlist platforms
        var platforms = new List<PlaylistPlatform>
        {
            new PlaylistPlatform { PlaylistId = playlists[0].Id, Platform = MusicPlatform.Spotify },
            new PlaylistPlatform { PlaylistId = playlists[0].Id, Platform = MusicPlatform.YouTube },
            new PlaylistPlatform { PlaylistId = playlists[1].Id, Platform = MusicPlatform.Spotify },
            new PlaylistPlatform { PlaylistId = playlists[2].Id, Platform = MusicPlatform.Spotify },
            new PlaylistPlatform { PlaylistId = playlists[2].Id, Platform = MusicPlatform.YouTubeMusic }
        };

        context.PlaylistPlatforms.AddRange(platforms);
        await context.SaveChangesAsync();
    }

    private static async Task SeedSongsAsync(MixocracyDbContext context)
    {
        var songs = new List<Song>
        {
            new Song
            {
                Id = Guid.Parse("ddddddd1-dddd-dddd-dddd-dddddddddddd"),
                Title = "Blinding Lights",
                Artist = "The Weeknd",
                Album = "After Hours",
                DurationMs = 200040,
                ReleaseYear = 2019,
                ISRC = "USUG11900839"
            },
            new Song
            {
                Id = Guid.Parse("eeeeeee2-eeee-eeee-eeee-eeeeeeeeeeee"),
                Title = "Good 4 U",
                Artist = "Olivia Rodrigo",
                Album = "SOUR",
                DurationMs = 178146,
                ReleaseYear = 2021,
                ISRC = "USDY11113445"
            },
            new Song
            {
                Id = Guid.Parse("fffffff3-ffff-ffff-ffff-ffffffffffff"),
                Title = "Levitating",
                Artist = "Dua Lipa",
                Album = "Future Nostalgia",
                DurationMs = 203064,
                ReleaseYear = 2020,
                ISRC = "GB29K2000024"
            },
            new Song
            {
                Id = Guid.Parse("1234567a-1234-1234-1234-123456789abc"),
                Title = "Stay",
                Artist = "The Kid LAROI & Justin Bieber",
                Album = "F*CK LOVE 3: OVER YOU",
                DurationMs = 141806,
                ReleaseYear = 2021,
                ISRC = "USCM52100070"
            },
            new Song
            {
                Id = Guid.Parse("abcdef12-abcd-abcd-abcd-abcdefabcdef"),
                Title = "Industry Baby",
                Artist = "Lil Nas X ft. Jack Harlow",
                Album = "MONTERO",
                DurationMs = 212120,
                ReleaseYear = 2021,
                ISRC = "USSM12104846"
            }
        };

        context.Songs.AddRange(songs);
        await context.SaveChangesAsync();

        // Add platform mappings
        var mappings = new List<SongPlatformMapping>
        {
            new SongPlatformMapping { SongId = songs[0].Id, Platform = MusicPlatform.Spotify, ExternalId = "0VjIjW4GlUZAMYd2vXMi3b", ExternalUrl = "https://open.spotify.com/track/0VjIjW4GlUZAMYd2vXMi3b" },
            new SongPlatformMapping { SongId = songs[1].Id, Platform = MusicPlatform.Spotify, ExternalId = "4ZtFanR9U6ndgddUvNcjcG", ExternalUrl = "https://open.spotify.com/track/4ZtFanR9U6ndgddUvNcjcG" },
            new SongPlatformMapping { SongId = songs[2].Id, Platform = MusicPlatform.Spotify, ExternalId = "39LLxExYz6ewLAcYrzQQyP", ExternalUrl = "https://open.spotify.com/track/39LLxExYz6ewLAcYrzQQyP" },
            new SongPlatformMapping { SongId = songs[3].Id, Platform = MusicPlatform.Spotify, ExternalId = "5PjdY0CKGZdEuoNab3yDmX", ExternalUrl = "https://open.spotify.com/track/5PjdY0CKGZdEuoNab3yDmX" },
            new SongPlatformMapping { SongId = songs[4].Id, Platform = MusicPlatform.Spotify, ExternalId = "27NovPIUIRrOZoCHxABJwK", ExternalUrl = "https://open.spotify.com/track/27NovPIUIRrOZoCHxABJwK" }
        };

        context.SongPlatformMappings.AddRange(mappings);

        // Add songs to playlists
        var playlistSongs = new List<PlaylistSong>
        {
            new PlaylistSong
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                SongId = songs[0].Id,
                AddedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Status = PlaylistSongStatus.Approved,
                Position = 1,
                ApprovedAt = DateTime.UtcNow.AddDays(-4)
            },
            new PlaylistSong
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                SongId = songs[1].Id,
                AddedByUserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Status = PlaylistSongStatus.Approved,
                Position = 2,
                ApprovedAt = DateTime.UtcNow.AddDays(-3)
            },
            new PlaylistSong
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                SongId = songs[2].Id,
                AddedByUserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Status = PlaylistSongStatus.Pending,
                Position = 3
            }
        };

        context.PlaylistSongs.AddRange(playlistSongs);
        await context.SaveChangesAsync();

        // Add some votes
        var votes = new List<Vote>
        {
            new Vote
            {
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                PlaylistSongId = playlistSongs[2].Id,
                Type = VoteType.Upvote,
                Action = VoteAction.Add
            },
            new Vote
            {
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                PlaylistSongId = playlistSongs[2].Id,
                Type = VoteType.Upvote,
                Action = VoteAction.Add
            }
        };

        context.Votes.AddRange(votes);
        await context.SaveChangesAsync();
    }

    private static async Task SeedActivitiesAsync(MixocracyDbContext context)
    {
        var activities = new List<PlaylistActivity>
        {
            new PlaylistActivity
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Type = ActivityType.PlaylistCreated,
                Description = "Created the playlist",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new PlaylistActivity
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Type = ActivityType.MemberJoined,
                Description = "Joined the playlist",
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new PlaylistActivity
            {
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Type = ActivityType.SongAdded,
                Description = "Added 'Blinding Lights' by The Weeknd",
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            }
        };

        context.PlaylistActivities.AddRange(activities);

        // Add some notifications
        var notifications = new List<Notification>
        {
            new Notification
            {
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Type = NotificationType.MemberJoined,
                Title = "New Member",
                Message = "Alice joined your playlist 'Friday Night Vibes'",
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new Notification
            {
                UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Type = NotificationType.VoteRequest,
                Title = "Vote Required",
                Message = "A new song needs your vote in 'Friday Night Vibes'",
                PlaylistId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        };

        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
    }
}