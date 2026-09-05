using System.Collections.Generic;

namespace Knox.Models
{
    public class KeyVaultsListModel
    {
        public List<KeyVaultModel> value { get; set; }
    }

    public class KeyVaultModel
    {
        public string name { get; set; }
    }
}
