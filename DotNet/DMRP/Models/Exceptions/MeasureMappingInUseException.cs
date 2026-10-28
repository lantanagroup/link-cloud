namespace LantanaGroup.Link.DMRP.Models.Exceptions
{
    /// <summary>
    /// A measure mapping could not be deleted because reporting plans still reference it. Kept
    /// distinct from a missing mapping so the two do not share a status code: the row plainly exists,
    /// and answering 404 for it tells the caller the opposite of what happened.
    /// </summary>
    public sealed class MeasureMappingInUseException : InvalidOperationException
    {
        public MeasureMappingInUseException(string id, Exception? innerException = null)
            : base($"Measure mapping {id} is referenced by one or more facility reporting plans and " +
                   "cannot be deleted. Remove those reporting plans first.", innerException)
        {
        }

        public MeasureMappingInUseException(Exception? innerException = null)
            : base("One or more measure mappings are referenced by facility reporting plans and " +
                   "cannot be deleted. Remove those reporting plans first.", innerException)
        {
        }
    }
}
