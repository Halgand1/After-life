using System;
using AfterCity.Core.Sim;
using AfterCity.Core.World;
using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Tutoriel optionnel de la premiere partie : quelques etapes courtes (bois, cailloux, barricade, nuit).
    /// « Passer » ou la fin l'enregistrent (PlayerPrefs) : il ne revient plus, sauf si on le rejoue depuis l'aide (H).
    /// </summary>
    public sealed class Tutorial : MonoBehaviour
    {
        const string DoneKey = "aftercity.tutorial.done";

        struct Step
        {
            public string Text;
            public Func<bool> Done;
        }

        GameRoot _game;
        Step[] _steps;
        int _index = -1;
        int _treesFelled;
        int _startPebbles;
        float _doneTimer;
        bool _disabled;

        public bool Active => _index >= 0 && !_disabled;
        public static bool AlreadyDone => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public void Init(GameRoot game)
        {
            _game = game;
            game.Colony.TreeFelled += _ => _treesFelled++;
            _steps = new[]
            {
                new Step { Text = "Coupe 2 arbres : onglet <b>Recolte</b>, outil <b>1</b>, puis clique ou glisse sur des arbres.",
                           Done = () => _treesFelled >= 2 },
                new Step { Text = "Ramasse des cailloux : <b>Recolte > 3</b> sur un petit tas gris pres de la ferme (4 cailloux = 1 pierre).",
                           Done = () => PebbleTotal() > _startPebbles },
                new Step { Text = "Construis une barricade : onglet <b>Defense</b>, outil <b>1</b>, puis clique sur une case (2 bois).",
                           Done = AnyWall },
                new Step { Text = "Prepare la nuit : creuse une fosse (<b>Terrain > 1</b>, deux fois au meme endroit) ou ajoute des barricades. Pret ? Appuie sur <b>N</b>.",
                           Done = () => _game.Colony.IsNight },
            };
        }

        int PebbleTotal() =>
            _game.Colony.Stock.Get(ResourceType.Pebble) + _game.Colony.Stock.Get(ResourceType.Stone) * Colony.PebblesPerStone;

        bool AnyWall()
        {
            var g = _game.Map.Grid;
            var c = _game.Map.FarmCenter;
            for (int y = -20; y <= 20; y++)
            for (int x = -20; x <= 20; x++)
            {
                var p = new GridPos(c.X + x, c.Y + y);
                if (g.InBounds(p) && g.HasWall(p)) return true;
            }
            return false;
        }

        /// <summary>Demarre le tutoriel s'il n'a jamais ete fait (ou si on force).</summary>
        public void StartIfNeeded(bool force = false)
        {
            if (_disabled || (!force && AlreadyDone)) return;
            _index = 0;
            _treesFelled = 0;
            _startPebbles = PebbleTotal();
            _doneTimer = 0;
        }

        /// <summary>Mode demo / mesure : jamais de tutoriel, rien d'enregistre.</summary>
        public void Disable()
        {
            _disabled = true;
            _index = -1;
        }

        void Finish()
        {
            _index = -1;
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        float _check;

        void Update()
        {
            if (!Active) return;
            if (_index >= _steps.Length)
            {
                _doneTimer += Time.unscaledDeltaTime;
                if (_doneTimer > 6f) Finish();
                return;
            }
            _check -= Time.unscaledDeltaTime;
            if (_check > 0) return;
            _check = 0.3f;
            if (_steps[_index].Done())
            {
                _index++;
                if (_index == 2) _startPebbles = PebbleTotal();
            }
        }

        /// <summary>Dessine le panneau (appele par le HUD). Retourne le rectangle utilise, ou null.</summary>
        public Rect? Draw(float w, float y, GUIStyle panel, GUIStyle title, GUIStyle text, GUIStyle button)
        {
            if (!Active) return null;
            var r = new Rect(w / 2 - 360, y, 720, 92);
            GUI.Box(r, GUIContent.none, panel);
            bool finished = _index >= _steps.Length;
            string head = finished ? "Tutoriel termine !" : $"Tutoriel  {_index + 1}/{_steps.Length}";
            GUI.Label(new Rect(r.x + 18, r.y + 8, 400, 26), head, title);
            string body = finished
                ? $"Bravo. La premiere horde arrive la nuit du jour {_game.Horde.FirstHordeDay}. Barricades, fosses et feu de camp : a toi de jouer."
                : _steps[_index].Text;
            GUI.Label(new Rect(r.x + 18, r.y + 36, 560, 52), body, text);
            if (!finished && GUI.Button(new Rect(r.xMax - 128, r.y + 10, 112, 32), "Passer", button)) Finish();
            return r;
        }
    }
}
