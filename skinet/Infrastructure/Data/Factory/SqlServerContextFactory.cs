namespace Infrastructure.Data.Factory;

public class SqlServerContextFactory : IDesignTimeDbContextFactory<SqlServerStoreContext>
{
    public SqlServerStoreContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SqlServerStoreContext>();
        optionsBuilder.UseSqlServer("Server=tcp:skinet-2025a.database.windows.net,1433;Initial Catalog=skinet;Persist Security Info=False;User ID=appuser;Password=Pa$$w0rd;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=300;");
        return new SqlServerStoreContext(optionsBuilder.Options);
    }
}
