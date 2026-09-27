using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// Recognises the database refusing a second measure mapping for one measure.
    /// </summary>
    /// <remarks>
    /// Two callers need this and they want opposite things from it, as with
    /// <see cref="UniquePeriodViolation"/>. The manager turns it into a duplicate error, because a
    /// caller who asked to create a mapping that already exists should be told so. The sync treats it
    /// as a lost race and reads back what the winner wrote, because it did not ask to create a
    /// particular row -- it asked for every measure DMRP named to be present, and another sync
    /// getting there first is a step towards that rather than away from it.
    /// </remarks>
    internal static class DuplicateMeasureMapping
    {
        // SQL Server: 2627 = unique constraint, 2601 = unique index. SQLite: 2067 = the extended
        // UNIQUE code. EF wraps the provider exception, sometimes several levels deep, so walk the
        // chain rather than looking only at InnerException.
        internal static bool Matches(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException { Number: 2601 or 2627 }
                    || current is SqliteException { SqliteExtendedErrorCode: 2067 })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
