namespace Infrastructure.Data.Factory;
public class SqliteContextFactory : IDesignTimeDbContextFactory<SqliteStoreContext>
{
    public SqliteStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteStoreContext>();
        // Relative to the project dir dotnet ef runs from, so it resolves to skinet/data.
        optionsBuilder.UseSqlite("Data Source=../../data/store.db;");
        return new SqliteStoreContext(optionsBuilder.Options);
    }
}