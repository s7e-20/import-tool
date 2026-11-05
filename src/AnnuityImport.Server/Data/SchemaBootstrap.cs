using Microsoft.Data.SqlClient;

namespace AnnuityImport.Server.Data;

public static class SchemaBootstrap
{
    public static void Ensure(IConfiguration configuration)
    {
        if (!configuration.GetValue("Database:EnsureSchema", true))
            return;

        var connectionString = configuration.GetConnectionString("CaseTransfert")
            ?? throw new InvalidOperationException("Connection string 'CaseTransfert' is missing.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new InvalidOperationException("The connection string must name a database.");

        var database = builder.InitialCatalog;
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

        using (var connection = new SqlConnection(master.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                IF DB_ID(@name) IS NULL
                BEGIN
                    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name);
                    EXEC(@sql);
                END
                """;
            command.Parameters.AddWithValue("@name", database);
            command.ExecuteNonQuery();
        }

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT OBJECT_ID(N'dbo.CASE_TRANSFERT', N'U')";
            var id = exists.ExecuteScalar();
            if (id is not null and not DBNull)
                return;

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "sql", "001-create-case-transfert.sql");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("The CASE_TRANSFERT creation script was not found.", scriptPath);

            using var create = connection.CreateCommand();
            create.CommandText = File.ReadAllText(scriptPath);
            create.ExecuteNonQuery();
        }
    }
}
