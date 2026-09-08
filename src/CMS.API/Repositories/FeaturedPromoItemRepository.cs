using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class FeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public FeaturedPromoItemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<FeaturedPromoItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => await QueryAsync(new FeaturedPromoItemQuery(), cancellationToken);

    public async Task<IEnumerable<FeaturedPromoItem>> QueryAsync(
        FeaturedPromoItemQuery query,
        CancellationToken cancellationToken = default)
    {
        var (where, parameters) = FeaturedPromoItemSql.BuildWhere(query);
        var sql = $"{FeaturedPromoItemSql.SelectBase}\n{where}\n{FeaturedPromoItemSql.DefaultOrderBy}";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await MultiMapAsync(connection, sql, new DynamicParameters(parameters), cancellationToken);
    }

    public async Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var items = await MultiMapAsync(
            connection,
            $"{FeaturedPromoItemSql.SelectBase}\nWHERE fpi.pkid = @Pkid",
            new DynamicParameters(new { Pkid = pkid }),
            cancellationToken);

        return items.SingleOrDefault();
    }

    public async Task<bool> SlotTakenAsync(
        DateOnly scheduleOn,
        short trainingCenterPkid,
        byte slot,
        int? excludePkid,
        CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM FeaturedPromoItem
                WHERE ScheduleOn = @ScheduleOn
                  AND TrainingCenter_pkid = @TrainingCenterPkid
                  AND Slot = @Slot
                  AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)
            ) THEN 1 ELSE 0 END
            """,
            new
            {
                ScheduleOn = scheduleOn,
                TrainingCenterPkid = trainingCenterPkid,
                Slot = slot,
                ExcludePkid = excludePkid,
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// pkid is int IDENTITY, so it is omitted from the INSERT and read back through
    /// SCOPE_IDENTITY(), which returns decimal and needs the explicit CAST.
    /// </summary>
    public async Task<int> CreateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO FeaturedPromoItem (ScheduleOn, TrainingCenter_pkid, Slot, Promotion_pkid, Topic, Description)
            VALUES (@ScheduleOn, @TrainingCenterPkid, @Slot, @PromotionPkid, @Topic, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            ToParameters(request),
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // pkid is the primary key and is not updatable.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE FeaturedPromoItem
            SET ScheduleOn = @ScheduleOn,
                TrainingCenter_pkid = @TrainingCenterPkid,
                Slot = @Slot,
                Promotion_pkid = @PromotionPkid,
                Topic = @Topic,
                Description = @Description
            WHERE pkid = @Pkid;
            """,
            ToParameters(request),
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Nothing in the schema references FeaturedPromoItem, so a plain DELETE is safe.
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    /// <summary>
    /// The three-step swap is forced by IX_FeaturedPromoItem_UniqueDateLocSlot: two rows can never
    /// hold the same slot, even mid-statement, so the neighbour is parked on slot 0 (a value the
    /// UI never assigns) while the moving row takes its place. All of it runs in one transaction so
    /// a failure cannot leave a row stranded on slot 0.
    /// </summary>
    public async Task<bool> MoveToSlotAsync(int pkid, byte targetSlot, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var current = await connection.QuerySingleOrDefaultAsync<SlotKey>(new CommandDefinition(
            "SELECT ScheduleOn, TrainingCenter_pkid AS TrainingCenterPkid, Slot FROM FeaturedPromoItem WHERE pkid = @Pkid",
            new { Pkid = pkid },
            transaction,
            cancellationToken: cancellationToken));

        if (current is null)
        {
            return false;
        }

        var neighbourPkid = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT pkid FROM FeaturedPromoItem
            WHERE ScheduleOn = @ScheduleOn AND TrainingCenter_pkid = @TrainingCenterPkid AND Slot = @Slot
            """,
            new { current.ScheduleOn, current.TrainingCenterPkid, Slot = targetSlot },
            transaction,
            cancellationToken: cancellationToken));

        if (neighbourPkid is int neighbour)
        {
            await SetSlotAsync(connection, transaction, neighbour, 0, cancellationToken);
            await SetSlotAsync(connection, transaction, pkid, targetSlot, cancellationToken);
            await SetSlotAsync(connection, transaction, neighbour, current.Slot, cancellationToken);
        }
        else
        {
            await SetSlotAsync(connection, transaction, pkid, targetSlot, cancellationToken);
        }

        transaction.Commit();
        return true;
    }

    private static Task<int> SetSlotAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        byte slot,
        CancellationToken cancellationToken)
        => connection.ExecuteAsync(new CommandDefinition(
            "UPDATE FeaturedPromoItem SET Slot = @Slot WHERE pkid = @Pkid",
            new { Pkid = pkid, Slot = slot },
            transaction,
            cancellationToken: cancellationToken));

    private static object ToParameters(FeaturedPromoItemRequest request) => new
    {
        request.Pkid,
        request.ScheduleOn,
        request.TrainingCenterPkid,
        request.Slot,
        request.PromotionPkid,
        Topic = request.Topic.Trim(),
        Description = request.Description.Trim(),
    };

    private static async Task<List<FeaturedPromoItem>> MultiMapAsync(
        IDbConnection connection,
        string sql,
        DynamicParameters parameters,
        CancellationToken cancellationToken)
    {
        var items = await connection
            .QueryAsync<FeaturedPromoItem, TrainingCenterLookup, PromotionLookup, FeaturedPromoItem>(
                new CommandDefinition(sql, parameters, cancellationToken: cancellationToken),
                (item, trainingCenter, promotion) =>
                {
                    item.TrainingCenter = trainingCenter;
                    item.Promotion = promotion;
                    return item;
                },
                splitOn: FeaturedPromoItemSql.SplitOn);

        return items.ToList();
    }

    /// <summary>The unique-key columns of the row being moved.</summary>
    private sealed record SlotKey(DateOnly ScheduleOn, short TrainingCenterPkid, byte Slot);
}
