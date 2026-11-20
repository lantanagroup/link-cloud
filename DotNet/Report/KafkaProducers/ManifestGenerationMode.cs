namespace LantanaGroup.Link.Report.KafkaProducers
{
    /// <summary>
    /// Who a manifest is being generated for. It decides whether
    /// <see cref="ReportManifestProducer.Generate"/> flags what is still in flight.
    /// </summary>
    public enum ManifestGenerationMode
    {
        /// <summary>
        /// The manifest that <see cref="ReportManifestProducer.Produce"/> uploads, and whose copy to the
        /// external container is the submission. It never carries in-flight warnings: an entry that
        /// changed after the completion gate passed must not leak an <c>incomplete</c> warning into
        /// what is submitted.
        /// </summary>
        Scheduled,

        /// <summary>
        /// A manifest generated on demand and returned to the caller, never uploaded. It flags every
        /// non-terminal entry, and warns when the reporting period has not closed.
        /// </summary>
        AdHoc
    }
}
