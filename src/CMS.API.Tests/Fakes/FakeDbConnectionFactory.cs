using System.Data;
using CMS.API.Data;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Hands out one scripted <see cref="FakeDbConnection"/>. A repository opens a connection per
/// method call and the same instance comes back every time, so a test reads the whole sequence of
/// statements off it afterwards.
/// </summary>
public sealed class FakeDbConnectionFactory : IDbConnectionFactory
{
    public FakeDbConnectionFactory(FakeDbConnection connection)
    {
        Connection = connection;
    }

    public FakeDbConnection Connection { get; }

    public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbConnection>(Connection);
}
