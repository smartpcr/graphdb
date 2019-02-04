namespace Common.DocDB
{
    public class CosmosDbSetting
    {
        public string AccountName { get; set; }
        public string DbName { get; set; }
        public string CollectionName { get; set; }
        public string AuthClientId { get; set; }
        public string AuthCertThumbprint { get; set; }
        public string VaultName { get; set; }
        public string DbKeySecret { get; set; }
    }
}
