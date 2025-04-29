
[Serializable]
internal class DataAcquisitionLogNotFoundException : Exception
{
    public DataAcquisitionLogNotFoundException()
    {
    }

    public DataAcquisitionLogNotFoundException(string? message) : base(message)
    {
    }

    public DataAcquisitionLogNotFoundException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}