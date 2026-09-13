using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database;
using PetChat.ApiServer.Database.Entities.Userspace;
using PetChat.ApiServer.Database.Repositories;
using PetChat.ApiServer.Services.Synchronization;
using PetChat.ApiServer.Tests.Integration.Fixtures;

namespace PetChat.ApiServer.Tests.Integration;

/// <summary>
/// <see cref="SnapshotService"/> - создает снимок актуального состояния событий юзера
/// Требует реального PostgreSQL: RepeatableRead/READ ONLY транзакции и
/// FromSqlInterpolated с оконной функцией ROW_NUMBER() не воспроизвести через
/// InMemory-провайдер EF Core.
/// </summary>

[Collection("Postgres")]
public class SnapshotServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetUserSnapshotAsync_UserWithNoChats_ReturnsEmptySnapshot_AndSequenceZero()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "sample-user");
        var sut = new SnapshotService(db);

        var (snapshot, sequence) = await sut.GetUserSnapshotAsync(owner.Id);

        Assert.Empty(snapshot.Chats);
        Assert.Empty(snapshot.Messages);
        Assert.Equal(0, sequence);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_ReturnsChat_WithPeerAttached()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");
        var peer = await CreateUserAsync(db, "peer");
        var chat = await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id);

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        var returnedChat = Assert.Single(snapshot.Chats);

        Assert.Equal(chat.Id, returnedChat.Id);
        Assert.NotNull(returnedChat.ChatProperties);
        Assert.NotNull(returnedChat.ChatProperties!.Peer);
        Assert.Equal(peer.Id, returnedChat.ChatProperties.Peer!.Id);
        Assert.Equal("peer", returnedChat.ChatProperties.Peer.Nickname);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_MessagesArray_ExcludesDeletedMessages()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");
        var peer = await CreateUserAsync(db, "peer");
        var chat = await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id);

        db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: 1, text: "живое сообщение"));
        db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: 2, text: "удалено", deleted: true));
        await db.SaveChangesAsync();

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        var message = Assert.Single(snapshot.Messages);
        Assert.Equal("живое сообщение", message.Text);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_LastMessageOnChat_ExcludesDeletedMessages()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");
        var peer = await CreateUserAsync(db, "peer");
        var chat = await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id);

        db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: 1, text: "первое"));
        db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: 2, text: "последнее, но удалено", deleted: true));
        await db.SaveChangesAsync();

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        var returnedChat = Assert.Single(snapshot.Chats);
        Assert.NotNull(returnedChat.LastMessage);
        Assert.Equal("первое", returnedChat.LastMessage.Text);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_MoreThan50MessagesInChat_ReturnsOnlyLast50ByIndex()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");
        var peer = await CreateUserAsync(db, "peer");
        var chat = await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id);

        for (int i = 1; i <= 60; i++)
            db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: i, text: $"msg-{i}"));
        await db.SaveChangesAsync();

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        Assert.Equal(50, snapshot.Messages.Length);
        // должны остаться сообщения с наибольшим Index (11..60), а не первые 50 (1..50).
        Assert.DoesNotContain(snapshot.Messages, m => m.Text == "msg-1");
        Assert.Contains(snapshot.Messages, m => m.Text == "msg-60");
    }

    [Fact]
    public async Task GetUserSnapshotAsync_SetsFromOwnerFlag_RelativeToRequestingUser()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");
        var peer = await CreateUserAsync(db, "peer");
        var chat = await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id);

        db.Messages.Add(MakeMessage(owner.Id, peer.Id, chat.Id, index: 1, text: "от владельца"));
        db.Messages.Add(MakeMessage(peer.Id, owner.Id, chat.Id, index: 2, text: "от собеседника"));
        await db.SaveChangesAsync();

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        var own = Assert.Single(snapshot.Messages, m => m.Text == "от владельца");
        var fromPeer = Assert.Single(snapshot.Messages, m => m.Text == "от собеседника");
        Assert.True(own.FromOwner);
        Assert.False(fromPeer.FromOwner);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_SequenceEqualsLatestUserEventSequence_AtCallTime()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner");

        var eventsRepo = new UserEventRepository(db);
        await eventsRepo.AppendAsync(owner.Id, null, "test", new { });
        var last = await eventsRepo.AppendAsync(owner.Id, null, "test", new { });

        var sut = new SnapshotService(db);
        var (_, sequence) = await sut.GetUserSnapshotAsync(owner.Id);

        Assert.Equal(last.Sequence, sequence);
    }

    [Fact]
    public async Task GetUserSnapshotAsync_MoreThan50Chats_ReturnsOnlyTop50ByLastActivity()
    {
        await using var db = fixture.CreateContext();
        var owner = await CreateUserAsync(db, "owner-51chats");

        var now = DateTime.UtcNow;
        for (int i = 0; i <= 50; i++)
        {
            var peer = await CreateUserAsync(db, $"peer-{i}");
            await CreateChatWithPropertiesAsync(db, owner.Id, peer.Id, lastActivityAt: now.AddMinutes(-i));
        }

        var sut = new SnapshotService(db);
        var (snapshot, _) = await sut.GetUserSnapshotAsync(owner.Id);

        Assert.Equal(50, snapshot.Chats.Length);
        // чат с юзером peer-50 не должен попасть в выборку, т.к. у него самое давнее LastASctivity
        Assert.DoesNotContain(snapshot.Chats, c => c.Id == Chat.GetChatId(owner.Id, GetPeerIdByNickname(db, "peer-50")));
    }

    #region helpers
    private static async Task<User> CreateUserAsync(PetChatDbContext db, string nickname)
    {
        var user = new User { Nickname = nickname };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Chat> CreateChatWithPropertiesAsync(
        PetChatDbContext db, int ownerId, int peerId, DateTime? lastActivityAt = null)
    {
        var chatId = Chat.GetChatId(ownerId, peerId);
        var chat = new Chat
        {
            Id = chatId,
            Members = [ownerId, peerId],
            LastActivityAt = lastActivityAt ?? DateTime.UtcNow
        };
        db.Chats.Add(chat);
        db.ChatProperties.Add(new ChatProperties { UserId = ownerId, PeerId = peerId, ChatId = chatId });
        db.ChatProperties.Add(new ChatProperties { UserId = peerId, PeerId = ownerId, ChatId = chatId });

        await db.SaveChangesAsync();
        return chat;
    }

    private static Message MakeMessage(int fromId, int peerId, string chatId, int index, string text, bool deleted = false) => new()
    {
        FromId = fromId,
        PeerId = peerId,
        ChatId = chatId,
        Index = index,
        Text = text,
        Deleted = deleted
    };

    private static int GetPeerIdByNickname(PetChatDbContext db, string nickname) =>
        db.Users.AsNoTracking().Single(u => u.Nickname == nickname).Id;
    #endregion
}
