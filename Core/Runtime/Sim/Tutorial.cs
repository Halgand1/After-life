using System;
using System.Collections.Generic;
using AfterCity.Core.World;

namespace AfterCity.Core.Sim
{
    /// <summary>Une consigne du tutoriel : texte court, puis une condition qui la valide.</summary>
    public sealed class TutorialStep
    {
        public string Title { get; }
        public string Hint { get; }
        internal readonly Func<bool> IsDone;

        internal TutorialStep(string title, string hint, Func<bool> isDone)
        {
            Title = title;
            Hint = hint;
            IsDone = isDone;
        }
    }

    /// <summary>
    /// Tutoriel facultatif (GDD 3) : consignes courtes pas a pas, validees par ce qui se passe vraiment dans la colonie.
    /// Le jeu decide s'il le propose (premiere partie) ; on peut le passer a tout moment.
    /// </summary>
    public sealed class Tutorial
    {
        readonly Colony _colony;
        readonly List<TutorialStep> _steps = new List<TutorialStep>();
        int _index;
        int _treesFelled, _walls;
        readonly int _startStone;

        public IReadOnlyList<TutorialStep> Steps => _steps;
        public int Index => _index;
        public TutorialStep Current => Finished ? null : _steps[_index];
        public bool Finished => _index >= _steps.Count;
        public bool Skipped { get; private set; }

        /// <summary>Une consigne vient d'etre reussie (consigne reussie, suivante ou null).</summary>
        public event Action<TutorialStep, TutorialStep> StepCompleted;
        public event Action Completed;

        public Tutorial(Colony colony)
        {
            _colony = colony;
            _startStone = colony.Stock.Get(ResourceType.Stone) * Colony.PebblesPerStone + colony.Stock.Get(ResourceType.Pebble);
            colony.TreeFelled += _ => _treesFelled++;
            colony.CellChanged += p => { if (colony.Grid.InBounds(p) && colony.Grid.HasWall(p)) _walls++; };

            Add("Coupe du bois", "Recolte > Couper du bois, puis glisse sur deux arbres.", () => _treesFelled >= 2);
            Add("Ramasse des cailloux", "Recolte > Ramasser des cailloux : quatre cailloux font une pierre.",
                () => Stones() >= _startStone + Colony.PebblesPerStone);
            Add("Pose une barricade", "Defense > Barricade, sur une case vide pres de la ferme (2 bois).", () => _walls >= 1);
            Add("Construis un abri", "Batiments > Abri : quatre places pour dormir.", () => HasHome());
            Add("Tiens jusqu'a la nuit", "Les rodeurs sortent la nuit. Garde tes survivants pres de la ferme.", () => colony.IsNight);
        }

        void Add(string title, string hint, Func<bool> done) => _steps.Add(new TutorialStep(title, hint, done));

        int Stones() => _colony.Stock.Get(ResourceType.Stone) * Colony.PebblesPerStone + _colony.Stock.Get(ResourceType.Pebble);

        bool HasHome()
        {
            foreach (var b in _colony.Buildings) if (b.Built && b.Home != null) return true;
            return false;
        }

        /// <summary>A appeler regulierement (chaque image ou chaque pas de simulation).</summary>
        public void Update()
        {
            while (!Finished && _steps[_index].IsDone())
            {
                var done = _steps[_index];
                _index++;
                StepCompleted?.Invoke(done, Current);
                if (Finished) Completed?.Invoke();
            }
        }

        /// <summary>Passer le tutoriel (bouton « Passer »).</summary>
        public void Skip()
        {
            if (Finished) return;
            Skipped = true;
            _index = _steps.Count;
            Completed?.Invoke();
        }
    }
}
