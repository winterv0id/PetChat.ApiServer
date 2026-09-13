using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database;
using Testcontainers.PostgreSql;

namespace PetChat.ApiServer.Tests.Integration.Fixtures;

/// <summary>
/// Один контейнер PostgreSQL на весь прогон интеграционных тестов.
/// Каждый тест использует уникальные userId/chatId (не через пересоздание БД на каждый метод)
/// Каждый класс тестов смещает userId на 100_000 (начинается с 100_000) и может создать 99_999 пользователей 
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("petchat_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        //реальная миграция бд
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public PetChatDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PetChatDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new PetChatDbContext(options);
    }
}

/// <summary>
/// Отмечает тестовые классы, которым нужен общий контейнер БД. 
/// xUnit гарантирует, что <see cref="PostgresFixture"/> будет создан 
/// один раз на всю коллекцию, а не по разу на каждый тестовый класс.
/// </summary>
[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
