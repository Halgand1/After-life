using System.Collections.Generic;

namespace AfterCity.Core.Sim
{
    /// <summary>Stocks de la colonie. Jamais négatifs.</summary>
    public sealed class Stockpile
    {
        readonly Dictionary<ResourceType, int> _amounts = new Dictionary<ResourceType, int>();

        public int Get(ResourceType type) => _amounts.TryGetValue(type, out var v) ? v : 0;

        public void Add(ResourceType type, int amount)
        {
            if (amount <= 0) return;
            _amounts[type] = Get(type) + amount;
        }

        public bool TrySpend(ResourceType type, int amount)
        {
            if (amount < 0 || Get(type) < amount) return false;
            _amounts[type] = Get(type) - amount;
            return true;
        }
    }
}
