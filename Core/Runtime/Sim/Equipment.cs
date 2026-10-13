using System;
using System.Collections.Generic;

namespace AfterCity.Core.Sim
{
    /// <summary>Roles de depart des survivants (fiches 3D) : chacun arrive avec sa dotation.</summary>
    public enum SurvivorRole { Worker, Quarrier, Militiaman, Soldier, Tracker, Medic, Cleaner }

    /// <summary>Emplacements d'equipement (GDD 5.5), accroches SOCKET_* du squelette.</summary>
    public enum EquipSlot { Head, Torso, Back, Belt, RightHand, LeftHand }

    /// <summary>Un type d'objet du catalogue (Art/Equipement/catalogue_equipement.json).</summary>
    public sealed class EquipmentSpec
    {
        public string Id { get; }
        public EquipSlot Slot { get; }
        /// <summary>Part des degats evitee (0 a 1) quand on est attaque.</summary>
        public double Protection { get; }
        /// <summary>Ordres acceleres par l'outil, et de combien (0,5 = 50 % plus vite).</summary>
        public IReadOnlyDictionary<JobType, double> WorkBonus { get; }
        /// <summary>Bonus aux chances de toucher a la chasse.</summary>
        public double HuntBonus { get; }
        /// <summary>Utilisations (outils) ou coups recus (protections) avant de casser.</summary>
        public int Durability { get; }

        EquipmentSpec(string id, EquipSlot slot, double protection = 0, double hunt = 0, int durability = 0, params (JobType job, double bonus)[] work)
        {
            Id = id;
            Slot = slot;
            Protection = protection;
            HuntBonus = hunt;
            var w = new Dictionary<JobType, double>();
            foreach (var (job, bonus) in work) w[job] = bonus;
            WorkBonus = w;
            Durability = durability > 0 ? durability : (protection > 0 ? 25 : w.Count > 0 || hunt > 0 ? 60 : 0);
        }

        static readonly Dictionary<string, EquipmentSpec> _all = new Dictionary<string, EquipmentSpec>();
        public static IEnumerable<EquipmentSpec> All => _all.Values;

        public static EquipmentSpec Get(string id) => _all.TryGetValue(id, out var s) ? s : null;

        static void Add(EquipmentSpec s) => _all[s.Id] = s;

        // Valeurs provisoires, a reprendre avec l'equilibrage (equilibrage/ ne couvre pas encore l'equipement)
        static EquipmentSpec()
        {
            Add(new EquipmentSpec("Casque_Militaire", EquipSlot.Head, protection: 0.15));
            Add(new EquipmentSpec("Casque_Police", EquipSlot.Head, protection: 0.12));
            Add(new EquipmentSpec("Casque_Chantier", EquipSlot.Head, protection: 0.05));
            Add(new EquipmentSpec("Casque_Moto", EquipSlot.Head, protection: 0.10));
            Add(new EquipmentSpec("Casquette", EquipSlot.Head));
            Add(new EquipmentSpec("Bonnet", EquipSlot.Head));
            Add(new EquipmentSpec("Chapeau_Pisteur", EquipSlot.Head));
            Add(new EquipmentSpec("Bandana", EquipSlot.Head));
            Add(new EquipmentSpec("Masque_Gaz", EquipSlot.Head));
            Add(new EquipmentSpec("Gilet_Tactique", EquipSlot.Torso, protection: 0.20));
            Add(new EquipmentSpec("Gilet_Police", EquipSlot.Torso, protection: 0.15));
            Add(new EquipmentSpec("Plastron_Bricole", EquipSlot.Torso, protection: 0.10));
            Add(new EquipmentSpec("Gilet_HauteVisibilite", EquipSlot.Torso));
            Add(new EquipmentSpec("Brassard_Medecin", EquipSlot.Torso));
            Add(new EquipmentSpec("Sac_Randonnee", EquipSlot.Back));
            Add(new EquipmentSpec("Sac_Petit", EquipSlot.Back));
            Add(new EquipmentSpec("Fusil_Dos", EquipSlot.Back));
            Add(new EquipmentSpec("Arbalete_Dos", EquipSlot.Back));
            Add(new EquipmentSpec("Hotte_Bois", EquipSlot.Back, work: (JobType.Chop, 0.15)));
            Add(new EquipmentSpec("Jerrican_Dos", EquipSlot.Back));
            Add(new EquipmentSpec("Ceinture_Outils", EquipSlot.Belt, work: new[] { (JobType.Construct, 0.15), (JobType.Barricade, 0.15) }));
            Add(new EquipmentSpec("Ceinture_Tactique", EquipSlot.Belt));
            Add(new EquipmentSpec("Ceinture_Survie", EquipSlot.Belt));
            Add(new EquipmentSpec("Ceinture_Medicale", EquipSlot.Belt));
            Add(new EquipmentSpec("Hache", EquipSlot.RightHand, work: (JobType.Chop, 0.5)));
            Add(new EquipmentSpec("Pioche", EquipSlot.RightHand, work: (JobType.Mine, 0.5)));
            Add(new EquipmentSpec("Pelle", EquipSlot.RightHand, work: new[] { (JobType.Dig, 0.5), (JobType.Raise, 0.5) }));
            Add(new EquipmentSpec("Marteau", EquipSlot.RightHand, work: new[] { (JobType.Construct, 0.3), (JobType.Barricade, 0.3) }));
            Add(new EquipmentSpec("Houe", EquipSlot.RightHand, work: new[] { (JobType.Road, 0.3), (JobType.Pick, 0.2) }));
            Add(new EquipmentSpec("Batte_Cloutee", EquipSlot.RightHand));
            Add(new EquipmentSpec("Machette", EquipSlot.RightHand, work: new[] { (JobType.Chop, 0.2), (JobType.Pick, 0.2) }));
            Add(new EquipmentSpec("Pied_De_Biche", EquipSlot.RightHand, work: new[] { (JobType.Salvage, 0.4), (JobType.Search, 0.4) }));
            Add(new EquipmentSpec("Fusil", EquipSlot.RightHand, hunt: 0.2));
            Add(new EquipmentSpec("Arbalete", EquipSlot.RightHand, hunt: 0.15));
            Add(new EquipmentSpec("Seau", EquipSlot.RightHand));
            Add(new EquipmentSpec("Sac_Medical", EquipSlot.LeftHand));
            Add(new EquipmentSpec("Lampe_Torche", EquipSlot.LeftHand));
            Add(new EquipmentSpec("Bouclier_Panneau", EquipSlot.LeftHand, protection: 0.10));
        }

        /// <summary>Dotation de depart de chaque role (catalogue_equipement.json > dotations).</summary>
        public static IReadOnlyList<string> Loadout(SurvivorRole role)
        {
            switch (role)
            {
                case SurvivorRole.Worker: return new[] { "Casquette", "Gilet_HauteVisibilite", "Hotte_Bois", "Ceinture_Outils", "Hache" };
                case SurvivorRole.Quarrier: return new[] { "Casque_Chantier", "Ceinture_Outils", "Pioche" };
                case SurvivorRole.Militiaman: return new[] { "Bandana", "Plastron_Bricole", "Ceinture_Survie", "Batte_Cloutee", "Bouclier_Panneau" };
                case SurvivorRole.Soldier: return new[] { "Casque_Militaire", "Gilet_Tactique", "Ceinture_Tactique", "Fusil" };
                case SurvivorRole.Tracker: return new[] { "Chapeau_Pisteur", "Arbalete_Dos", "Ceinture_Survie", "Machette", "Lampe_Torche" };
                case SurvivorRole.Medic: return new[] { "Brassard_Medecin", "Sac_Petit", "Ceinture_Medicale", "Sac_Medical" };
                case SurvivorRole.Cleaner: return new[] { "Masque_Gaz", "Gilet_HauteVisibilite", "Ceinture_Survie", "Pied_De_Biche" };
                default: return Array.Empty<string>();
            }
        }

        /// <summary>Nom du modele 3D du role (SK_Survivant_&lt;nom&gt;).</summary>
        public static string ModelName(SurvivorRole role)
        {
            switch (role)
            {
                case SurvivorRole.Worker: return "Ouvrier";
                case SurvivorRole.Quarrier: return "Carrier";
                case SurvivorRole.Militiaman: return "Milicien";
                case SurvivorRole.Soldier: return "Militaire";
                case SurvivorRole.Tracker: return "Pisteur";
                case SurvivorRole.Medic: return "Medecin";
                default: return "Nettoyeur";
            }
        }
    }

    /// <summary>Un objet precis, porte ou range, avec son usure.</summary>
    public sealed class EquipmentItem
    {
        public EquipmentSpec Spec { get; }
        public string Id => Spec.Id;
        public EquipSlot Slot => Spec.Slot;
        /// <summary>Utilisations ou coups restants ; 0 = casse.</summary>
        public int Durability { get; internal set; }
        public bool Wears => Spec.Durability > 0;

        public EquipmentItem(EquipmentSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Durability = spec.Durability;
        }
    }

    /// <summary>Ce que porte un survivant : un objet au plus par emplacement.</summary>
    public sealed class Loadout
    {
        readonly Dictionary<EquipSlot, EquipmentItem> _items = new Dictionary<EquipSlot, EquipmentItem>();

        public EquipmentItem this[EquipSlot slot] => _items.TryGetValue(slot, out var i) ? i : null;
        public IEnumerable<EquipmentItem> Items => _items.Values;

        /// <summary>Pose l'objet et rend celui qui occupait l'emplacement (ou null).</summary>
        internal EquipmentItem Put(EquipmentItem item)
        {
            var old = this[item.Slot];
            _items[item.Slot] = item;
            return old;
        }

        internal EquipmentItem Take(EquipSlot slot)
        {
            var old = this[slot];
            _items.Remove(slot);
            return old;
        }

        /// <summary>Protection totale, plafonnee a 50 %.</summary>
        public double Protection
        {
            get { double p = 0; foreach (var i in _items.Values) p += i.Spec.Protection; return Math.Min(0.5, p); }
        }

        public double WorkBonus(JobType job)
        {
            double b = 0;
            foreach (var i in _items.Values)
                if (i.Spec.WorkBonus.TryGetValue(job, out var x)) b += x;
            return b;
        }

        public double HuntBonus
        {
            get { double b = 0; foreach (var i in _items.Values) b += i.Spec.HuntBonus; return b; }
        }
    }
}
