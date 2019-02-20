using System;

namespace Common.DocDB
{
    public class CosmosDbSetting
    {
        public string AccountName { get; set; }
        public string DbName { get; set; }
        public string CollectionName { get; set; }
        public string AuthKey { get; set; }
        public int TimeoutInSeconds { get; set; } = 10;
        public int MaxRetryWaitTimeInSeconds { get; set; }
        public int MaxRetryAttemptsOnThrottledRequests { get; set; }
    }
}
