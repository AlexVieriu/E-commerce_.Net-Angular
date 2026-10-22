namespace Infrastructure.Data.Factory;

public class SqlServerContextFactory : IDesignTimeDbContextFactory<SqlServerStoreContext>
{
    // Used by `dotnet ef` at design time; reads the connection string from appsettings.json or env vars.
    public SqlServerStoreContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetBasePath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SqlServerAzureConnection");

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Connection string 'SqlServerAzureConnection' not found in configuration.");

        var optionsBuilder = new DbContextOptionsBuilder<SqlServerStoreContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new SqlServerStoreContext(optionsBuilder.Options);
    }

    private static void TestConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand("SELECT 1", connection);
            command.ExecuteScalar();

            Console.WriteLine("Database connection test successful.");
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException($"Failed to connect to the database. SQL Error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to connect to the database. Error: {ex.Message}", ex);
        }
    }

    private static string GetBasePath()
    {
        // Walks up from the current directory to the first appsettings.json.
        var currentDir = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDir);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? currentDir;
    }
}
