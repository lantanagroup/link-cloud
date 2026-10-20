namespace LantanaGroup.Link.Shared.Application.Models
{
    /// <summary>
    /// The names health checks are registered under. A registration name reaches the Admin UI's
    /// Health Checks page verbatim, so these are the labels it renders per row.
    /// </summary>
    /// <remarks>
    /// <see cref="HealthCheckType"/> cannot carry a provider-specific name: its members supply the
    /// registration name through <c>ToString()</c>, and an enum member cannot spell one. A database
    /// row says which provider it reached so that it can be compared against the Java services' --
    /// MeasureEval persists to MongoDB (CosmosDB for MongoDB RU in deployed environments), every
    /// .NET service to SQL Server -- rather than every row reading "Database" and leaving the
    /// provider to be inferred from the service.
    /// </remarks>
    public static class HealthCheckNames
    {
        /// <summary>
        /// A database check against SQL Server, which every .NET service persists to through EF Core.
        /// </summary>
        public const string SqlServerDatabase = "Database (SQL Server)";
    }
}
