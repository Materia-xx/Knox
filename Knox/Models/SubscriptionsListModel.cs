using System.Collections.Generic;

namespace Knox.Models
{
    public class SubscriptionsListModel
    {
        public List<SubscriptionModel> value { get; set; }
    }

    public class SubscriptionModel
    {
        public string subscriptionId { get; set; }
        public string displayName { get; set; }
    }
}
