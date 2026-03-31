namespace Infrastructure.Data.Factory;
public class SqliteContextFactory : IDesignTimeDbContextFactory<SqliteStoreContext>
{
    public SqliteStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqliteStoreContext>();
        optionsBuilder.UseSqlite("Data Source=../../../data/store.db;");
        return new SqliteStoreContext(optionsBuilder.Options);
    }
}