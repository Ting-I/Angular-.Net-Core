using System.Data;

namespace CMS.API.Data;

/// <summary>Creates open ADO.NET connections for Dapper.</summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
