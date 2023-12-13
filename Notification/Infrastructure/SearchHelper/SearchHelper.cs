namespace LantanaGroup.Link.Notification.Infrastructure.SearchHelper
{
    public static class SearchHelper
    {
        public static DateTime? StartOfDay(DateTime? theDate)
        {
            return theDate?.Date;
        }

        public static DateTime? EndOfDay(DateTime? theDate)
        {
            return theDate?.Date.AddDays(1).AddTicks(-1);
        }       
    }
}
