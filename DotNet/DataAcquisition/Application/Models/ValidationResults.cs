namespace LantanaGroup.Link.DataAcquisition.Application.Models;

public class ValidationResults
{
    public ValidationResults()
    {
        IsSuccess = true;
        ErrorMessages = new List<string>();
    }
    public bool IsSuccess { get; set; }
    public List<string> ErrorMessages { get; set; }
}
