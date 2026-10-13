using System;
using System.Collections.Generic;
using AfterCity.Core.Util;

namespace AfterCity.Core.Sim
{
    /// <summary>Destination d'expedition en ville.</summary>
    public sealed class ExpeditionTarget
    {
        public string Name { get; }
        /// <summary>Duree aller-retour en secondes de jeu.</summary>
        public double Duration { get; }
        /// <summary>Danger de 0 (calme) a 3 (tres dangereux) : baisse la reussite.</summary>
        public int Danger { get; }
        /// <summary>Butin maximal (mission parfaite).</summary>
        public IReadOnlyDictionary<ResourceType, int> Loot { get; }

        public ExpeditionTarget(string name, double duration, int danger, Dictionary<ResourceType, int> loot)
        {
            Name = name;
            Duration = duration;
            Danger = danger;
            Loot = loot;
        }

        public static readonly ExpeditionTarget[] All =
        {
            new ExpeditionTarget("Station-service", 40, 0, new Dictionary<ResourceType, int>
                { { ResourceType.Food, 8 }, { ResourceType.Scrap, 6 }, { ResourceType.Plastic, 4 } }),
            new ExpeditionTarget("Supermarche pille", 60, 1, new Dictionary<ResourceType, int>
                { { ResourceType.Food, 25 }, { ResourceType.Medicine, 3 }, { ResourceType.Plastic, 6 } }),
            new ExpeditionTarget("Quartier residentiel", 80, 2, new Dictionary<ResourceType, int>
                { { ResourceType.Food, 12 }, { ResourceType.Wood, 15 }, { ResourceType.Medicine, 5 }, { ResourceType.Electronics, 4 } }),
            new ExpeditionTarget("Zone industrielle", 110, 3, new Dictionary<ResourceType, int>
                { { ResourceType.Scrap, 25 }, { ResourceType.Steel, 10 }, { ResourceType.Electronics, 8 }, { ResourceType.Rubber, 6 } }),
        };
    }

    /// <summary>Une expedition en cours ou terminee.</summary>
    public sealed class Expedition
    {
        public const int MinSuccess = 50;
        public const int MaxSuccess = 95;
        public const int MaxMembers = 4;

        public ExpeditionTarget Target { get; }
        public IReadOnlyList<Villager> Members { get; }
        /// <summary>Chances de reussite en % (50 a 95), fixees au depart.</summary>
        public int SuccessChance { get; }
        public double Remaining { get; internal set; }
        public bool Done => Remaining <= 0;

        // Resultat (rempli au retour)
        /// <summary>Qualite de la mission de 0 (desastre) a 1 (parfaite).</summary>
        public double Outcome { get; internal set; }
        public bool Succeeded { get; internal set; }
        public Dictionary<ResourceType, int> Gains { get; } = new Dictionary<ResourceType, int>();
        public double DamageTaken { get; internal set; }

        internal Expedition(ExpeditionTarget target, List<Villager> members)
        {
            Target = target;
            Members = members;
            SuccessChance = ComputeChance(target, members);
            Remaining = target.Duration;
        }

        /// <summary>
        /// 50 % au minimum (regle de conception). Plus de membres et de bonne sante : mieux. Plus de danger : moins bien.
        /// </summary>
        public static int ComputeChance(ExpeditionTarget target, IReadOnlyList<Villager> members)
        {
            if (members.Count == 0) return MinSuccess;
            double health = 0;
            foreach (var m in members) health += m.Health / Villager.MaxHealth;
            health /= members.Count;
            double chance = 70 + 8 * (members.Count - 1) - 12 * target.Danger + 15 * (health - 1);
            return (int)Math.Round(Math.Max(MinSuccess, Math.Min(MaxSuccess, chance)));
        }

        /// <summary>
        /// Tire le resultat : la qualite depend du pourcentage de reussite. Reussie, elle rapporte beaucoup et blesse peu ;
        /// ratee, elle rapporte peu et blesse davantage. Personne ne meurt en expedition (pour l'instant).
        /// </summary>
        internal void Resolve(Rng rng)
        {
            double p = SuccessChance / 100.0;
            double roll = rng.NextDouble();
            Succeeded = roll < p;
            // Qualite : reussite => entre 0,6 et 1 (meilleure si la chance etait haute) ; echec => entre 0,1 et 0,5
            Outcome = Succeeded ? 0.6 + 0.4 * rng.NextDouble() * (0.5 + 0.5 * p)
                                : 0.15 + 0.35 * rng.NextDouble();
            Outcome = Math.Max(0, Math.Min(1, Outcome));
            foreach (var kv in Target.Loot)
            {
                int n = (int)Math.Round(kv.Value * Outcome * (0.8 + 0.4 * rng.NextDouble()));
                if (n > 0) Gains[kv.Key] = n;
            }
            DamageTaken = (1 - Outcome) * (25 + 15 * Target.Danger);
        }
    }
}
