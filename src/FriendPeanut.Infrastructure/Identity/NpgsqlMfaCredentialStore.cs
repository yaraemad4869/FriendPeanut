using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Persistence;

public sealed class NpgsqlMfaCredentialStore : IMfaCredentialStore
{
    private readonly NpgsqlDataSource _ds;
    public NpgsqlMfaCredentialStore(NpgsqlDataSource ds) { _ds = ds; }

    public async Task<MfaCredentialRecord?> FindForUserAsync(Guid userId, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("select id,user_id,secret_ciphertext,encryption_key_id,is_confirmed,last_accepted_time_step from friend_peanut.mfa_credentials where user_id=@u");
        cmd.Parameters.AddWithValue("u", userId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new MfaCredentialRecord(r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetBoolean(4), r.IsDBNull(5) ? null : r.GetInt64(5));
    }
    public async Task UpsertAsync(Guid userId, string secretCiphertext, string encryptionKeyId, bool confirmed, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("insert into friend_peanut.mfa_credentials (user_id,secret_ciphertext,encryption_key_id,is_confirmed,confirmed_at) values (@u,@s,@k,@c,case when @c then now() else null end) on conflict (user_id) do update set secret_ciphertext=excluded.secret_ciphertext, encryption_key_id=excluded.encryption_key_id, is_confirmed=excluded.is_confirmed, confirmed_at=case when excluded.is_confirmed then now() else null end, version=friend_peanut.mfa_credentials.version+1");
        cmd.Parameters.AddWithValue("u", userId); cmd.Parameters.AddWithValue("s", secretCiphertext);
        cmd.Parameters.AddWithValue("k", encryptionKeyId); cmd.Parameters.AddWithValue("c", confirmed);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task RecordAcceptedTimeStepAsync(Guid userId, long timeStep, CancellationToken ct)
    {
        await using var cmd = _ds.CreateCommand("update friend_peanut.mfa_credentials set last_accepted_time_step=@t, version=version+1 where user_id=@u");
        cmd.Parameters.AddWithValue("t", timeStep); cmd.Parameters.AddWithValue("u", userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}