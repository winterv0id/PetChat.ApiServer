namespace PetChat.ApiServer.Database;

using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Entities.Serverspace;
using PetChat.ApiServer.Database.Entities.Userspace;

public class PetChatDbContext(DbContextOptions<PetChatDbContext> options) : DbContext(options)
{
    public DbSet<UserCreditionals> UserCreditionals => Set<UserCreditionals>();
    public DbSet<UserEvent> UserEvents => Set<UserEvent>();
    public DbSet<UserAckState> UserAckStates => Set<UserAckState>();
    public DbSet<UserRelations> UserRelations => Set<UserRelations>();
    public DbSet<UserFcmRegistration> UserFcmRegistrations => Set<UserFcmRegistration>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ChatProperties> ChatProperties => Set<ChatProperties>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEvent>(e =>
        {
            e.ToTable("user_events");
            e.HasKey(ue => new { ue.UserId, ue.Sequence });
            e.Property(ue => ue.Sequence).UseIdentityAlwaysColumn();
            e.Property(ue => ue.PayloadJson).HasColumnType("jsonb");
            e.HasIndex(ue => new { ue.UserId, ue.Sequence }); // под GetEventsSinceAsync
        });

        modelBuilder.Entity<UserAckState>(e =>
        {
            e.ToTable("users_ack_state");
            e.HasKey(uas => uas.UserId);
            e.Property(uas => uas.UserId).ValueGeneratedNever();
        });

        modelBuilder.Entity<UserCreditionals>(e =>
        {
            e.ToTable("user_creditionals");
            e.HasKey(uc=> uc.Id);
            e.HasAlternateKey(uc => uc.Login);
            e.HasIndex(uc => uc.Login).IsUnique();
        });
        
        modelBuilder.Entity<Chat>(e =>
        {
            e.ToTable("chats");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Ignore(c => c.LastMessage);
            e.Ignore(c => c.ChatProperties);
        });

        modelBuilder.Entity<ChatProperties>(e =>
        {
            e.ToTable("chat_properties");
            e.HasOne(c => c.Peer).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasKey(cp => cp.Id);
            e.HasIndex(cp => new { cp.UserId, cp.ChatId }).IsUnique();
            e.Ignore(cp => cp.Peer);
        });
        
        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.HasOne<Chat>().WithMany().HasForeignKey(m => m.ChatId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.ChatId, m.Index });
            e.HasKey(m => m.Id);
            e.Ignore(m => m.FromOwner);            
        });

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.ShortName).IsUnique();
            e.Ignore(u => u.IsOnline);
        });

        modelBuilder.Entity<UserRelations>(e =>
        {
            e.ToTable("user_relations");
            e.HasKey(ur => ur.UserId);
            e.Property(ur => ur.UserId).ValueGeneratedNever();
        });

        modelBuilder.Entity<UserFcmRegistration>(e =>
        {
            e.ToTable("user_fcm_registrations");
            e.HasKey(x => new { x.UserId, x.InstallationId });
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.InstallationId).HasColumnName("installation_id");
            e.Property(x => x.RegisteredAt).HasColumnName("registered_at");

            // к каким пользователям привязана установка
            e.HasIndex(x => x.InstallationId);
        });
    }
}