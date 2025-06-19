namespace Infrastructure.Data.Factory;

public class SqliteStoreContext : StoreContext
{
    public SqliteStoreContext(DbContextOptions<SqliteStoreContext> options) : base(options) { }
}
