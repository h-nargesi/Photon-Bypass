using System.Data;
using Microsoft.Data.SqlClient;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Tools;

namespace PhotonBypass.DeployCli;

/// <summary>
/// Seeds the single first-release admin account. The structure scripts carry no
/// admin seed anymore (docs/09): the admin is created at deploy time with the
/// operator's real password, with the exact flags of the former seed —
/// Role = Admin (full access bypass, docs/20 D2) and UserType = AllowSelfService
/// (wallet/renewal gates, docs/20 D11). Exactly one admin is allowed (docs/20 D9).
/// </summary>
public static class AdminSeeder
{
    public enum CreateStatus
    {
        Created,
        DuplicateUsername,
        AdminAlreadyExists,
    }

    public static CreateStatus CreateAdmin(string connectionString, string username, string password)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @status varchar(30);
            IF EXISTS (SELECT 1 FROM dbo.Account WHERE Username = @username)
                SET @status = 'duplicate-username';
            ELSE IF EXISTS (SELECT 1 FROM dbo.Account WHERE Role = @role)
                SET @status = 'admin-exists';
            ELSE
            BEGIN
                INSERT INTO dbo.Account (Username, Password, Role, UserType)
                VALUES (@username, @hash, @role, @userType);
                SET @status = 'created';
            END
            SELECT @status;
            """;
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.VarChar, 255) { Value = username });
        command.Parameters.Add(new SqlParameter("@hash", SqlDbType.VarChar, 200) { Value = PasswordHasher.Hash(password) });
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.TinyInt) { Value = (byte)AccountRole.Admin });
        command.Parameters.Add(new SqlParameter("@userType", SqlDbType.TinyInt) { Value = (byte)UserTypes.AllowSelfService });

        return (string)command.ExecuteScalar()! switch
        {
            "created" => CreateStatus.Created,
            "duplicate-username" => CreateStatus.DuplicateUsername,
            "admin-exists" => CreateStatus.AdminAlreadyExists,
            var status => throw new InvalidOperationException($"AdminSeeder returned an unknown status '{status}'."),
        };
    }

    public static bool Verify(string connectionString, string username, string password)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Password FROM dbo.Account WHERE Username = @username;";
        command.Parameters.Add(new SqlParameter("@username", SqlDbType.VarChar, 255) { Value = username });

        return command.ExecuteScalar() as string is { } stored && PasswordHasher.Verify(password, stored);
    }
}
