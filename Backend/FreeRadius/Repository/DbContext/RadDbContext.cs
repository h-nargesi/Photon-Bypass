using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using PhotonBypass.Infra.Database;
using System.Data;

namespace PhotonBypass.FreeRadius.Repository.DbContext;

class RadDbContext(IOptions<RadDapperOptions> options) : IDapperDbContext
{
    private readonly MySqlConnection connection = new(options.Value.ConnectionString);

    public IDbConnection Connection => connection;

    public Task Open()
    {
        return connection.State == ConnectionState.Open ? Task.CompletedTask : connection.OpenAsync();
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
