using Microsoft.Data.SqlClient;

namespace NVGI.Projet.Importtool.Data;

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

        EnsureDatabase(connectionString, builder.InitialCatalog);
        EnsureTable(connectionString, "dbo.CASE_TRANSFERT", "001-create-case-transfert.sql");
        EnsureTable(connectionString, "dbo.[USER]", "002-create-user.sql");
    }

    private static void EnsureDatabase(string connectionString, string database)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        using var connection = new SqlConnection(master.ConnectionString);
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

    private static void EnsureTable(string connectionString, string objectName, string scriptFile)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT OBJECT_ID(@name, N'U')";
        exists.Parameters.AddWithValue("@name", objectName);
        var id = exists.ExecuteScalar();
        if (id is not null and not DBNull)
            return;

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "sql", scriptFile);
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"The {objectName} creation script was not found.", scriptPath);

        using var create = connection.CreateCommand();
        create.CommandText = File.ReadAllText(scriptPath);
        create.ExecuteNonQuery();
    }
}
