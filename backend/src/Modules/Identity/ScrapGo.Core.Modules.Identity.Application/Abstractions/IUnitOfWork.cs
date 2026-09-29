namespace ScrapGo.Core.Modules.Identity.Application.Abstractions;

/// <summary>The Identity module's transaction boundary. Audit rows staged via <see cref="IAuditLog{TModule}"/> commit with it.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction. If one is already
    /// open, it joins that ambient transaction instead of starting a nested
    /// one, which Npgsql doesn't support.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
