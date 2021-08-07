using System.Collections.Generic;

namespace Knox
{
    public class VaultRegistration
    {
        public string TenantId { get; set; }
        public string ClientId { get; set; }
        public string RedirectUri { get; set; }
        public List<string> VaultNames { get; set; }
    }
}
