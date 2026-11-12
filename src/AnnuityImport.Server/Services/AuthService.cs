using Microsoft.Data.SqlClient;

namespace NVGI.Projet.Importtool.Services;

public sealed class AuthService
{
    private readonly string _connectionString;

    public AuthService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("CaseTransfert")
            ?? throw new InvalidOperationException("Connection string 'CaseTransfert' is missing.");
    }

    public async Task<(bool Ok, string? Error)> ValidateAsync(string? login, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(login) || password is null)
            return (false, "Enter a login and password.");

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.[USER]
                    WHERE [singleSignOnLogin] = @login
                      AND [PASSUSER] = @password
                ) THEN 1 ELSE 0 END
                """;
            command.Parameters.Add("@login", System.Data.SqlDbType.NVarChar, 256).Value = login.Trim();
            command.Parameters.Add("@password", System.Data.SqlDbType.NVarChar, 256).Value = password;
            var matched = await command.ExecuteScalarAsync(cancellationToken);
            var ok = matched is not null and not DBNull && Convert.ToInt32(matched) == 1;
            return ok ? (true, null) : (false, "Login or password is incorrect.");
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return (false, "The USER table was not found.");
        }
        catch (SqlException ex) when (ex.Number == 207)
        {
            return (false, "The USER table does not have singleSignOnLogin and PASSUSER columns.");
        }
        catch (Exception ex)
        {
            return (false, "Could not reach SQL Server. " + ex.Message);
        }
    }
}
