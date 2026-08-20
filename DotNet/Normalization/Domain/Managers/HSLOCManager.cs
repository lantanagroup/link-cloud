﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Manager;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace LantanaGroup.Link.Normalization.Domain.Managers
{
    public interface IHSLOCManager
    {
        Task Update(string oldVersion, string newVersion, Stream csv);
        Task DeleteAll();
        Task DeleteByVersion(string version);
        Task DeleteById(Guid id);
    }

    public class HSLOCManager : IHSLOCManager
    {
        private readonly NormalizationDbContext _dbContext;
        private readonly ILogger<HSLOCManager> _logger;

        public HSLOCManager(NormalizationDbContext dbContext, ILogger<HSLOCManager> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task Update(string oldVersion, string newVersion, Stream csv)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(oldVersion);
                ArgumentException.ThrowIfNullOrWhiteSpace(newVersion);
                ArgumentNullException.ThrowIfNull(csv);

                if (!csv.CanRead)
                {
                    throw new ArgumentException("The HSLOC CSV stream must be readable.", nameof(csv));
                }

                var importedRows = ParseCsv(csv);
                var importedCodes = importedRows
                    .Select(row => row.HSLOCCode)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var oldRows = await _dbContext.HSLOCS
                    .Where(row => row.Version == oldVersion)
                    .ToListAsync();
                var oldRowsByCode = oldRows.ToDictionary(row => row.HSLOCCode, StringComparer.OrdinalIgnoreCase);

                foreach (var importedRow in importedRows)
                {
                    if (oldRowsByCode.TryGetValue(importedRow.HSLOCCode, out var oldRow))
                    {
                        oldRow.CDCCode = importedRow.CDCCode;
                        oldRow.ShortDescription = importedRow.ShortDescription;
                        oldRow.LongDescription = importedRow.LongDescription;
                        oldRow.Version = newVersion;
                        oldRow.IsActive = true;
                    }
                    else
                    {
                        _dbContext.HSLOCS.Add(new HSLOC
                        {
                            CDCCode = importedRow.CDCCode,
                            ShortDescription = importedRow.ShortDescription,
                            HSLOCCode = importedRow.HSLOCCode,
                            LongDescription = importedRow.LongDescription,
                            Version = newVersion,
                            IsActive = true
                        });
                    }
                }

                foreach (var oldRow in oldRows.Where(row => !importedCodes.Contains(row.HSLOCCode)))
                {
                    oldRow.IsActive = false;
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to update HSLOC data from CSV.");
                throw;
            }
        }

        private static string GetRequiredField(string[] fields, int index, string columnName, long lineNumber)
        {
            var value = fields[index].Trim();

            return value == null
                ? throw new ArgumentException($"The HSLOC CSV column '{columnName}' is required at line {lineNumber}.", "csv")
                : value;
        }

        public async Task DeleteAll()
        {
            try
            {
                await _dbContext.HSLOCS.ExecuteDeleteAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete all HSLOC records.");
                throw;
            }
        }

        public async Task DeleteByVersion(string version)
        {
            try
            {
                await _dbContext.HSLOCS.Where(q => q.Version == version).ExecuteDeleteAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete HSLOC records for the specified version.");
                throw;
            }
        }

        public async Task DeleteById(Guid id)
        {
            try
            {
                await _dbContext.HSLOCS.Where(q => q.Id == id).ExecuteDeleteAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete the specified HSLOC record.");
                throw;
            }
        }

        private static List<HSLOC> ParseCsv(Stream csv)
        {
            var expectedHeaders = new[]
            {
                nameof(HSLOC.CDCCode),
                nameof(HSLOC.ShortDescription),
                nameof(HSLOC.HSLOCCode),
                nameof(HSLOC.LongDescription),
            };

            using var parser = new TextFieldParser(csv)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");

            try
            {
                var headers = parser.ReadFields()
                    ?? throw new ArgumentException("The HSLOC CSV must include a header row.", nameof(csv));
                var columnIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < headers.Length; index++)
                {
                    var header = headers[index].Trim().TrimStart('\uFEFF');

                    if (!columnIndexes.TryAdd(header, index))
                    {
                        throw new ArgumentException($"The HSLOC CSV contains the duplicate column '{header}'.", nameof(csv));
                    }
                }

                if (columnIndexes.Count != expectedHeaders.Length || expectedHeaders.Any(header => !columnIndexes.ContainsKey(header)))
                {
                    throw new ArgumentException(
                        $"The HSLOC CSV must contain exactly these columns: {string.Join(", ", expectedHeaders)}.",
                        nameof(csv));
                }

                var importedRows = new List<HSLOC>();
                var importedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (!parser.EndOfData)
                {
                    var fields = parser.ReadFields();

                    if (fields is null || fields.Length != headers.Length)
                    {
                        throw new ArgumentException($"The HSLOC CSV row at line {parser.LineNumber} does not contain the expected number of columns.", nameof(csv));
                    }

                    var row = new HSLOC
                    {
                        CDCCode = GetRequiredField(fields, columnIndexes[nameof(HSLOC.CDCCode)], nameof(HSLOC.CDCCode), parser.LineNumber),
                        ShortDescription = GetRequiredField(fields, columnIndexes[nameof(HSLOC.ShortDescription)], nameof(HSLOC.ShortDescription), parser.LineNumber),
                        HSLOCCode = GetRequiredField(fields, columnIndexes[nameof(HSLOC.HSLOCCode)], nameof(HSLOC.HSLOCCode), parser.LineNumber),
                        LongDescription = GetRequiredField(fields, columnIndexes[nameof(HSLOC.LongDescription)], nameof(HSLOC.LongDescription), parser.LineNumber)
                    };

                    if (!importedCodes.Add(row.HSLOCCode))
                    {
                        throw new ArgumentException($"The HSLOC CSV contains the duplicate HSLOC code '{row.HSLOCCode}'.", nameof(csv));
                    }

                    importedRows.Add(row);
                }

                return importedRows;
            }
            catch (MalformedLineException exception)
            {
                throw new ArgumentException($"The HSLOC CSV is malformed at line {exception.LineNumber}.", nameof(csv), exception);
            }
        }
    }
}