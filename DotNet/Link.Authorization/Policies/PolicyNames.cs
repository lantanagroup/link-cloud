namespace Link.Authorization.Policies
{
    public static class PolicyNames
    {
        public const string IsLinkAdmin = "IsLinkAdmin";
        public const string CanViewInfrastructure = "CanViewInfrastructure";
        public const string CanManageKafkaTopics = "CanManageKafkaTopics";
        public const string CanManageScaling = "CanManageScaling";
        public const string CanOperateKafka = "CanOperateKafka";
    }
}
