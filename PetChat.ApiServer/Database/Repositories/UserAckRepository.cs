using Microsoft.EntityFrameworkCore;
using PetChat.ApiServer.Database.Repositories.Interfaces;

namespace PetChat.ApiServer.Database.Repositories;

public class UserAckRepository(PetChatDbContext petChatDb) : IUserAckRepository
{
    private readonly PetChatDbContext _petChatDb = petChatDb;
    
    public async Task WriteIfGreaterAsync(int userId, long sequence, CancellationToken ct = default)
    {
        // GREATEST — атомарная защита от отката ack назад
        await _petChatDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users_ack_state ("UserId", "AcknowledgedSequence", "AcknowledgedAt")
            VALUES ({userId}, {sequence}, now())
            ON CONFLICT ("UserId") DO UPDATE
            SET "AcknowledgedSequence" = GREATEST(users_ack_state."AcknowledgedSequence", {sequence}),
                "AcknowledgedAt" = now()
            """, ct);
    }
}