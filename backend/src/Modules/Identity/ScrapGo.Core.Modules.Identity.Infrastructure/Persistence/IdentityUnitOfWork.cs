using Npgsql;

namespace ScrapGo.Core.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            // Surfaced as a provider-neutral exception so Application handlers
            // can map a lost uniqueness race to a typed outcome (e.g. 409)
            // without referencing Npgsql.
            throw new UniqueConstraintViolationException(postgres.ConstraintName, ex);
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        // Npgsql/EF Core doesn't support nested transactions on one
        // connection. If a caller already opened one (e.g.
        // CreateOrganizationHandler provisioning its creator), join it, so
        // everything, audit rows included, commits or rolls back together.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // Disposing without a commit (the operation threw) rolls back.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
