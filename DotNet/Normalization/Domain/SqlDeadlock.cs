namespace LantanaGroup.Link.Normalization.Domain;

internal static class SqlDeadlock
{
    public const int MaxAttempts = 8;

    public static bool IsDeadlock(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.GetType().Name == "SqlException"
                && current.GetType().GetProperty("Number")?.GetValue(current) is int number
                && number == 1205)
            {
                return true;
            }

            if (current.Message.Contains("was deadlocked", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Retries a read that is not inside a caller transaction. A facility search takes shared
    /// locks under locking reads and can be the victim of a parallel create; the operations
    /// API otherwise returns that victim as HTTP 500.
    /// </summary>
    public static async Task<T> ExecuteReadAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsDeadlock(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(25, 80) * attempt), cancellationToken);
            }
        }
    }
}
