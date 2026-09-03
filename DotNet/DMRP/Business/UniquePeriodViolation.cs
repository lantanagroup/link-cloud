using LantanaGroup.Link.DMRP.Data.Repository.Mappings;
using Microsoft.Data.SqlClient;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Recognises the database refusing a second reporting plan for one facility, component,
    /// measure and period.
    /// </summary>
    /// <remarks>
    /// Two callers need this and they want opposite things from it. The manager turns it into a
    /// <c>409</c>, because a caller who asked to create a duplicate should be told so. The sync
    /// treats it as a lost race and reconciles, because it did not ask to create anything in
    /// particular -- it asked for the table to match what DMRP says, and another sync getting
    /// there first is a step towards that rather than away from it.
    /// </remarks>
    internal static class UniquePeriodViolation
    {
        // SQL Server: 2627 = unique constraint, 2601 = unique index. EF wraps the provider
        // exception, sometimes several levels deep, so walk the chain.
        internal static bool Matches(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException { Number: 2601 or 2627 })
                {
                    return true;
                }

                if (current.Message.Contains(FacilityReportingPlanConfigMap.UniquePeriodIndexName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // SQLite names the table and columns rather than the index, so the check above
                // cannot see it. The tests run on SQLite, and a race this code claims to handle
                // ought to be provable rather than asserted.
                if (current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                    && current.Message.Contains(nameof(Data.Entities.FacilityReportingPlan) + "s",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
