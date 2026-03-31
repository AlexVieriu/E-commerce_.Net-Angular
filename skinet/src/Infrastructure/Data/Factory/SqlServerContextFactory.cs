using Microsoft.Data.SqlClient;

namespace Infrastructure.Data.Factory;

public class SqlServerContextFactory : IDesignTimeDbContextFactory<SqlServerStoreContext>
{
    // public SqlServerStoreContext CreateDbContext(string[] args)
    // {
    //     var optionsBuilder = new DbContextOptionsBuilder<SqlServerStoreContext>();
    //     optionsBuilder.UseSqlServer("Server=tcp:skinet-2025a.database.windows.net,1433;Initial Catalog=skinet89;Persist Security Info=False;User ID=appuser;Password=Pa$$w0rd;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=100;");
    //     return new SqlServerStoreContext(optionsBuilder.Options);
    // }

    public SqlServerStoreContext CreateDbContext(string[] args)
    {
        // Build configuration to read from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(GetBasePath())
            .AddJsonFile("appsettings.json")
            .Build();

        // Get the connection string
        var connectionString = configuration.GetConnectionString("SqlServerAzureConnection");

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Connection string 'SqlServerAzureConnection' not found in configuration.");

        // Test the connection before creating the context
        TestConnection(connectionString);

        var optionsBuilder = new DbContextOptionsBuilder<SqlServerStoreContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new SqlServerStoreContext(optionsBuilder.Options);
    }

    private void TestConnection(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            // Optional: Test with a simple query to ensure the database is accessible
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

    private string GetBasePath()
    {
        // Look for appsettings.json in current directory or parent directories
        var currentDir = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDir);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? currentDir;
    }
}
