namespace GitHubProxy.Config
{
    public class ProxyConfig
    {
        public string Token { get; set; }
        public int Interval { get; set; }
        public ProxyAuthor DefaultAuthor { get; set; }
        public SourceProxyRepo Source { get; set; }
        public DestProxyRepo Destination { get; set; }
    }

    public class ProxyAuthor
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    internal interface IProxyRepo
    {
        string User { get; set; }
        string Repo { get; set; }
    }

    public class SourceProxyRepo : IProxyRepo
    {
        public string[] Branches { get; set; }
        public string User { get; set; }
        public string Repo { get; set; }
    }

    public class DestProxyRepo : IProxyRepo
    {
        public bool Anonymize { get; set; }
        public string User { get; set; }
        public string Repo { get; set; }
    }
}
