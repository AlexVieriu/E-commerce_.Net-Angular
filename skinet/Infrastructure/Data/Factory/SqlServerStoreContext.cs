namespace Infrastructure.Data.Factory;

public class SqlServerStoreContext : StoreContext
{
    public SqlServerStoreContext(DbContextOptions<SqlServerStoreContext> options) : base(options) { }
}
