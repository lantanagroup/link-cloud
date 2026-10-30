using System.Collections;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.IO.Enumeration;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Shared.Application.Auditing
{
    /// <summary>
    /// Captures what an update actually changed, as old and new value pairs, by listening to the
    /// save rather than asking the handler.
    /// </summary>
    /// <remarks>
    /// The change tracker already knows which properties differ, so this reads them there rather
    /// than asking the handler, which would describe what it meant to change. A masked property is
    /// still reported, with <see cref="AuditOptions.MaskedValue"/> on both sides.
    /// <para>
    /// A change reaches <see cref="IAuditContext"/> only once the database has kept it - its save
    /// succeeded, or the transaction it was made in committed - so a retried unit of work is
    /// described once. The service attaches it inside its own AddDbContext; it alters nothing and
    /// blocks nothing. What it cannot capture is in dev-docs/auditing-known-limits.md.
    /// </para>
    /// </remarks>
    public sealed class AuditPropertyChangeInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
    {
        //attributes never change at runtime, and reflecting on every save would dominate the request.
        private static readonly ConcurrentDictionary<(Type Entity, string Property), bool> MaskedByAttribute = new();

        private readonly IAuditContext _audit;
        private readonly AuditOptions _options;
        private readonly ILogger<AuditPropertyChangeInterceptor> _logger;

        //what the save in progress would change, held back until the database accepts it.
        private readonly Captured _saving = new();

        //what saves inside the open transaction changed, held back until that transaction commits.
        private readonly Captured _uncommitted = new();

        public AuditPropertyChangeInterceptor(IAuditContext audit, IOptions<AuditOptions> options,
            ILogger<AuditPropertyChangeInterceptor> logger)
        {
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            Saved(eventData.Context);
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Saved(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _saving.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            _saving.Clear();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesCanceledAsync(DbContextEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _saving.Clear();
            return base.SaveChangesCanceledAsync(eventData, cancellationToken);
        }

        public override void SaveChangesCanceled(DbContextEventData eventData)
        {
            _saving.Clear();
            base.SaveChangesCanceled(eventData);
        }

        //a new transaction means whatever an earlier one held was never committed.
        public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction result)
        {
            _uncommitted.Clear();
            return result;
        }

        public ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection,
            TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
        {
            _uncommitted.Clear();
            return ValueTask.FromResult(result);
        }

        public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
            Keep(_uncommitted);

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Keep(_uncommitted);
            return Task.CompletedTask;
        }

        public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
            _uncommitted.Clear();

        public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            _uncommitted.Clear();
            return Task.CompletedTask;
        }

        //a failed commit or rollback kept nothing. A failed savepoint is inside a transaction that
        //may still commit, so it is left alone.
        public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        {
            if (EndsTheTransaction(eventData))
            {
                _uncommitted.Clear();
            }
        }

        public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TransactionFailed(transaction, eventData);
            return Task.CompletedTask;
        }

        private static bool EndsTheTransaction(TransactionErrorEventData eventData) =>
            eventData.Action is "Commit" or "Rollback";

        /// <summary>
        /// The save succeeded. Outside a transaction that is the end of it; inside one, the change
        /// still depends on the commit.
        /// </summary>
        private void Saved(DbContext? context)
        {
            if (context?.Database.CurrentTransaction is null)
            {
                Keep(_saving);
            }
            else
            {
                _uncommitted.Absorb(_saving);
            }
        }

        private void Keep(Captured captured)
        {
            _audit.PropertyChanges.AddRange(captured.Changes);
            captured.Clear();
        }

        /// <summary>
        /// Reads the change tracker. Anything that goes wrong here costs the event its property
        /// changes, never the user their write.
        /// </summary>
        private void Capture(DbContext? context)
        {
            //whatever an earlier save left here was already kept or discarded when it finished.
            _saving.Clear();

            //a save no audited request wraps - a listener's or a job's - would be captured for nobody.
            if (context is null || !_audit.IsRecording)
            {
                return;
            }

            try
            {
                foreach (var entry in context.ChangeTracker.Entries())
                {
                    CaptureEntry(entry);
                }
            }
            catch (Exception ex)
            {
                _saving.Clear();
                _logger.LogWarning(ex,
                    "Audit property capture failed; the audit event will carry no property changes.");
            }
        }

        private void CaptureEntry(EntityEntry entry)
        {
            //a create has nothing to compare against, and a delete ends the record rather than
            //changing it. The ADR asks for property changes on updates only.
            if (entry.State != EntityState.Modified)
            {
                return;
            }

            var entityType = entry.Metadata.ClrType;

            foreach (var property in entry.Properties)
            {
                if (!property.IsModified)
                {
                    continue;
                }

                //keys identify the row rather than describe it, and the modify timestamp moves on
                //every save, so it would appear in every event.
                if (property.Metadata.IsPrimaryKey() || property.Metadata.IsForeignKey())
                {
                    continue;
                }

                if (_options.TimestampPropertyNames.Contains(property.Metadata.Name))
                {
                    continue;
                }

                //an Update() marks every property modified, whether or not its value differs.
                if (Equals(property.OriginalValue, property.CurrentValue))
                {
                    continue;
                }

                var converter = property.Metadata.GetValueConverter();
                var initial = Describe(converter, property.OriginalValue);
                var current = Describe(converter, property.CurrentValue);

                //a converted property whose converter has no comparer is compared by reference, so a
                //new list holding the same items arrives marked modified. Collections mapped without
                //a converter never get here - EF compares those by their contents itself.
                if (initial == current)
                {
                    continue;
                }

                //a masked value was described only to learn whether it changed. Neither form is kept.
                _saving.Changes.Add(IsMasked(entityType, property)
                    ? new PropertyChangeModel(property.Metadata.Name, AuditOptions.MaskedValue, AuditOptions.MaskedValue)
                    : new PropertyChangeModel(property.Metadata.Name, initial, current));
            }
        }

        /// <summary>
        /// A value as text, in the form it is stored in.
        /// </summary>
        /// <remarks>
        /// Two values that differ must never read the same, and one value must read the same on
        /// every host. ToString fails both: a collection mapped without a converter reads as its
        /// type on both sides of a change, and a decimal or a date follows the host's culture.
        /// </remarks>
        private static string Describe(ValueConverter? converter, object? value)
        {
            if (value is null)
            {
                return string.Empty;
            }

            var stored = converter is null ? value : converter.ConvertToProvider(value);

            return stored switch
            {
                null => string.Empty,
                string text => text,

                //"O" for a date, so the offset and the sub-second part survive; invariant for the rest.
                IFormattable formattable => formattable.ToString(
                    formattable is DateTime or DateTimeOffset ? "O" : null, CultureInfo.InvariantCulture),

                //by runtime type, so a byte[] reads as base64. A throw is caught with the rest of the
                //capture, costing the event its changes and never the user their write.
                IEnumerable items => JsonSerializer.Serialize<object>(items),

                _ => stored.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// Changes the database has not yet confirmed.
        /// </summary>
        private sealed class Captured
        {
            public List<PropertyChangeModel> Changes { get; } = [];

            public void Absorb(Captured other)
            {
                Changes.AddRange(other.Changes);
                other.Clear();
            }

            public void Clear() => Changes.Clear();
        }

        /// <summary>
        /// Whether this property's values must never leave the service, and so are masked: marked
        /// on the entity, named in the masking list, or caught by one of its patterns.
        /// </summary>
        private bool IsMasked(Type entityType, PropertyEntry property)
        {
            var name = property.Metadata.Name;

            var marked = MaskedByAttribute.GetOrAdd((entityType, name),
                _ => property.Metadata.PropertyInfo?.IsDefined(typeof(AuditMaskAttribute), inherit: true) == true);
            if (marked)
            {
                return true;
            }

            if (_options.MaskedPropertyNames.Contains(name))
            {
                return true;
            }

            foreach (var pattern in _options.MaskedPropertyPatterns)
            {
                if (FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
