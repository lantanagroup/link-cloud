using CsvHelper.Configuration.Attributes;

namespace Terminology.Application.Models;

public class CsvCodeSystemRecord
{
    [Index(0)]
    public string Code { get; set; }
    
    [Index(1)]
    public string Display { get; set; }
}