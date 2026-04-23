namespace ProxyShellReady.Models
{
    public enum RuleEntryType
    {
        Domain,
        DomainSuffix,
        ProcessName
    }

    public class RuleEntry
    {
        public RuleEntryType Type { get; set; }
        public string Value { get; set; }
        public string Service { get; set; } = "Unknown";
        public bool IsEnabled { get; set; } = true;
    }
}
