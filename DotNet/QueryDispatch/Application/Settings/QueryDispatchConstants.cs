namespace QueryDispatch.Application.Settings
{
    public static class QueryDispatchConstants
    {
        public const string ServiceName = "QueryDispatch";

        /// <summary>
        /// The type of event that the dispatch should be triggered after.
        /// </summary>
        public enum EventType
        {
            Discharge
        }

        public static class AppSettingsSectionNames
        {
            public const string ServiceInformation = "ServiceInformation";
        }

        public static class LoggingIds
        {
            public const int GenerateItems = 1000;
            public const int SearchPerformed = 1001;
            public const int GetItem = 1002;
            public const int InsertItem = 1003;
            public const int UpdateItem = 1004;
            public const int DeleteItem = 1005;
            public const int GetItemNotFound = 1006;
            public const int UpdateItemNotFound = 1007;
            public const int SearchException = 1008;
            public const int GetItemException = 1009;
            public const int EventConsumerInit = 2000;
            public const int EventConsumerObserved = 2001;
            public const int EventConsumerException = 2002;
            public const int EventConsumerOperationCanceled = 2003;
            public const int HealthCheck = 10010;
        }
    }
}
