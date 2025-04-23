using CsvHelper.Configuration.Attributes;

namespace Terminology.Application.Models;

public class CsvValueSetRecord
{
    [Index(0)]
    public string System { get; set; }
    
    [Index(1)]
    public string Code { get; set; }
    
    [Index(2)]
    public string Display { get; set; }
}