namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    public class CodeMap
    {
        public string Code { get; private set; }
        public string Display { get; private set; }
        public CodeMap(string code, string display)
        {
            Code = code;
            Display = display;
        }
    }
}
