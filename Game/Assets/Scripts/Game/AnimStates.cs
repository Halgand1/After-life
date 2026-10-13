using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Choisit le nom d'etat d'animation disponible dans l'Animator : d'abord les etats des nouveaux modeles
    /// (Repos, Marche, Hache...), sinon ceux des modeles de test (ANIM_Villager_*, ANIM_Zombie_*).
    /// </summary>
    public static class AnimStates
    {
        public static readonly string[] Idle = { "Repos", "ANIM_Villager_Idle" };
        public static readonly string[] Walk = { "Marche", "ANIM_Villager_Walk" };
        public static readonly string[] Run = { "Course", "Marche", "ANIM_Villager_Walk" };
        public static readonly string[] Chop = { "Frapper", "Hache" };
        public static readonly string[] Mine = { "Piocher", "ANIM_Villager_Mine" };
        public static readonly string[] Gather = { "RamasserCailloux", "Ramasser", "ANIM_Villager_Gather" };
        public static readonly string[] Pick = { "Cueillir", "RamasserCailloux", "ANIM_Villager_Gather" };
        public static readonly string[] Search = { "Fouiller", "RamasserCailloux", "ANIM_Villager_Gather" };
        public static readonly string[] Hit = { "Frapper" };

        public static readonly string[] ZombieIdle = { "Repos", "ANIM_Zombie_Idle" };
        public static readonly string[] ZombieWalk = { "Marche", "ANIM_Zombie_Walk" };
        public static readonly string[] ZombieAttack = { "Attaque", "Repos", "ANIM_Zombie_Idle" };

        /// <summary>Premier etat existant de la liste, ou null.</summary>
        public static string Resolve(Animator animator, string[] candidates)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return null;
            foreach (var name in candidates)
                if (animator.HasState(0, Animator.StringToHash(name))) return name;
            return null;
        }
    }
}
