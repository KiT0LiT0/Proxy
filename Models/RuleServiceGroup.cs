using System.Collections.Generic;

namespace ProxyShellReady.Models
{
    public class RuleServiceGroup
    {
        public string Name { get; set; } = "Other";
        public List<RuleEntry> Entries { get; set; } = new List<RuleEntry>();
        public int Count
        {
            get { return Entries.Count; }
        }
    }
}
