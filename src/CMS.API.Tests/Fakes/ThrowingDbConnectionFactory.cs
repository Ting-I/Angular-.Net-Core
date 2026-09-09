using System.Data;
using CMS.API.Data;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="IDbConnectionFactory"/> in the in-process host so the rule that tests
/// never touch SQL Server is enforced rather than merely intended: a request that reaches a
/// repository nobody replaced with a fake fails loudly here instead of quietly opening a
/// connection to the developer's database.
/// </summary>
public class ThrowingDbConnectionFactory : IDbConnectionFactory
{
    public Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "A test reached the database. Replace the repository it used with a fake.");
}
