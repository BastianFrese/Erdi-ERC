using Microsoft.EntityFrameworkCore;

namespace Erdi_ERC.Services
{
    /// <summary>
    /// Helper service to handle database transactions safely with atomic operations
    /// </summary>
    public class DatabaseTransactionHelper
    {
        private readonly ILogger<DatabaseTransactionHelper> _logger;

        public DatabaseTransactionHelper(ILogger<DatabaseTransactionHelper> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Execute an operation within a database transaction with automatic rollback on error
        /// </summary>
        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            DbContext context,
            Func<Task<TResult>> operation,
            string operationName = "Database Operation")
        {
            using (var tx = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    var result = await operation();
                    await context.SaveChangesAsync();
                    await tx.CommitAsync();
                    _logger.LogInformation("{OperationName} completed successfully", operationName);
                    return result;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning(ex, "{OperationName} failed due to concurrency conflict", operationName);
                    throw;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "{OperationName} failed with error", operationName);
                    throw;
                }
            }
        }

        /// <summary>
        /// Execute an operation within a database transaction without returning a result
        /// </summary>
        public async Task ExecuteInTransactionAsync(
            DbContext context,
            Func<Task> operation,
            string operationName = "Database Operation")
        {
            using (var tx = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    await operation();
                    await context.SaveChangesAsync();
                    await tx.CommitAsync();
                    _logger.LogInformation("{OperationName} completed successfully", operationName);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning(ex, "{OperationName} failed due to concurrency conflict", operationName);
                    throw;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "{OperationName} failed with error", operationName);
                    throw;
                }
            }
        }

        /// <summary>
        /// Toggle an item (add if not exists, remove if exists) within a transaction
        /// </summary>
        public async Task<bool> ExecuteToggleOperationAsync<TEntity>(
            DbContext context,
            Func<Task<TEntity?>> findExisting,
            Action<TEntity> onRemove,
            Action onAdd,
            string operationName = "Toggle Operation")
            where TEntity : class
        {
            using (var tx = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    var existing = await findExisting();
                    bool wasAdded;

                    if (existing is null)
                    {
                        onAdd();
                        wasAdded = true;
                    }
                    else
                    {
                        onRemove(existing);
                        wasAdded = false;
                    }

                    await context.SaveChangesAsync();
                    await tx.CommitAsync();
                    _logger.LogInformation("{OperationName} completed successfully (wasAdded={WasAdded})", operationName, wasAdded);
                    return wasAdded;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning(ex, "{OperationName} failed due to concurrency conflict", operationName);
                    throw;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    _logger.LogError(ex, "{OperationName} failed with error", operationName);
                    throw;
                }
            }
        }
    }
}
