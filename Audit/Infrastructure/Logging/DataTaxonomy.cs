using Microsoft.Extensions.Compliance.Classification;

namespace LantanaGroup.Link.Audit.Infrastructure.Logging
{
    public static class DataTaxonomy
    {
        public static string TaxonomyName { get; } = typeof(DataTaxonomy).FullName!;

        public static DataClassification SensitiveData { get; } = new(TaxonomyName, nameof(SensitiveData));
        public static DataClassification PiiData { get; } = new(TaxonomyName, nameof(PiiData));
        public static DataClassification PhiData { get; } = new(TaxonomyName, nameof(PhiData));
    }

    public class SensitiveDataAttribute : DataClassificationAttribute
    {
        public SensitiveDataAttribute() : base(DataTaxonomy.SensitiveData)
        {
        }
    }

    //prefix key = 808
    public class PiiDataAttribute : DataClassificationAttribute
    {
        public PiiDataAttribute() : base(DataTaxonomy.PiiData)
        {
        }
    }

    public class PhiDataAttribute : DataClassificationAttribute
    {
        public PhiDataAttribute() : base(DataTaxonomy.PhiData)
        {
        }
    }
}


