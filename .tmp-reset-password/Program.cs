using Microsoft.AspNetCore.Identity;
using Npgsql;

const string connectionString = "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
const string email = "platform.owner@ecosys.test";
const string password = "EcosysAdmin!2345";

var hasher = new PasswordHasher<BootstrapUser>();
var hash = hasher.HashPassword(new BootstrapUser(), password);

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

await using var command = connection.CreateCommand();
command.CommandText = """
    update users
    set "PasswordHash" = @hash,
        "FailedLoginCount" = 0,
        "LastFailedLoginAt" = null,
        "LockedOutUntil" = null,
        "MustChangePassword" = false
    where lower("Email") = lower(@email)
    returning "Email";
    """;
command.Parameters.AddWithValue("hash", hash);
command.Parameters.AddWithValue("email", email);

var updatedEmail = await command.ExecuteScalarAsync();
Console.WriteLine(updatedEmail is null
    ? "No user matched the requested email."
    : $"Password reset for {updatedEmail}.");

sealed class BootstrapUser { }
