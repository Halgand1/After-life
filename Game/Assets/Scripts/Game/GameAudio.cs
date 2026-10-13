using UnityEngine;

namespace AfterCity.Game
{
    /// <summary>
    /// Sons provisoires generes par le code (pas de fichiers audio) : hache, marteau, pelle, grognements,
    /// sirene de horde, vent le jour et grillons la nuit. Seront remplaces par de vrais sons plus tard.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        public AudioClip Chop { get; private set; }
        public AudioClip Hammer { get; private set; }
        public AudioClip Shovel { get; private set; }
        public AudioClip Groan { get; private set; }
        public AudioClip Horn { get; private set; }
        public AudioClip Fall { get; private set; }

        const int Rate = 44100;
        AudioSource[] _pool;
        int _next;
        AudioSource _wind, _crickets, _ui;
        GameRoot _game;
        System.Random _rng = new System.Random(7);

        public void Init(GameRoot game)
        {
            Instance = this;
            _game = game;
            Chop = Make("Chop", 0.18f, (t, n) => (n * 0.6f + Mathf.Sin(t * 2 * Mathf.PI * 110f) * 0.8f) * Mathf.Exp(-t * 28f));
            Hammer = Make("Hammer", 0.25f, (t, n) => (Mathf.Sin(t * 2 * Mathf.PI * 880f) * 0.5f + Mathf.Sin(t * 2 * Mathf.PI * 1330f) * 0.3f + n * 0.3f) * Mathf.Exp(-t * 22f));
            Shovel = Make("Shovel", 0.3f, (t, n) => n * 0.7f * Mathf.Exp(-t * 12f) * (1f - Mathf.Exp(-t * 200f)), lowPass: 0.15f);
            Groan = Make("Groan", 1.3f, (t, n) =>
            {
                float f = 85f + Mathf.Sin(t * 2 * Mathf.PI * 3f) * 8f - t * 15f;
                float saw = 2f * (t * f - Mathf.Floor(t * f + 0.5f));
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.3f) * Mathf.PI);
                return (saw * 0.6f + n * 0.15f) * env;
            }, lowPass: 0.08f);
            Horn = Make("Horn", 1.8f, (t, n) =>
            {
                float f = t < 0.9f ? 220f : 175f;
                float s = Mathf.Sin(t * 2 * Mathf.PI * f) + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * f * 2f) + 0.25f * Mathf.Sin(t * 2 * Mathf.PI * f * 3f);
                float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.8f - t) * 4f);
                return s * 0.35f * env;
            });
            Fall = Make("Fall", 0.9f, (t, n) => (n * 0.5f * Mathf.Exp(-t * 5f) + Mathf.Sin(t * 2 * Mathf.PI * 60f) * Mathf.Exp(-t * 6f) * 0.7f) * Mathf.Clamp01(t * 20f), lowPass: 0.2f);

            _pool = new AudioSource[16];
            for (int i = 0; i < _pool.Length; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0.75f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 15f;
                s.maxDistance = 140f;
                _pool[i] = s;
            }
            _wind = Loop("Wind", 6f, BrownNoise(6f, 0.02f), 0.18f);
            _crickets = Loop("Crickets", 3f, CricketData(3f), 0f);
            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;
        }

        delegate float Synth(float t, float noise);

        AudioClip Make(string name, float seconds, Synth f, float lowPass = 1f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float x = f(t, (float)(_rng.NextDouble() * 2 - 1));
                y += (x - y) * lowPass;
                data[i] = Mathf.Clamp(y, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        float[] BrownNoise(float seconds, float step)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            float v = 0;
            for (int i = 0; i < n; i++)
            {
                v = Mathf.Clamp(v + (float)(_rng.NextDouble() * 2 - 1) * step, -1f, 1f) * 0.999f;
                float gust = 0.6f + 0.4f * Mathf.Sin(i / (float)n * Mathf.PI * 2f * 2f);
                d[i] = v * gust;
            }
            // Fondu aux extremites pour une boucle sans clic
            for (int i = 0; i < 2000; i++) { float k = i / 2000f; d[i] *= k; d[n - 1 - i] *= k; }
            return d;
        }

        static float[] CricketData(float seconds)
        {
            int n = (int)(seconds * Rate);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float chirp = (t % 0.6f) < 0.12f && ((t * 40f) % 1f) < 0.5f ? 1f : 0f;
                d[i] = Mathf.Sin(t * 2 * Mathf.PI * 4400f) * chirp * 0.25f;
            }
            return d;
        }

        AudioSource Loop(string name, float seconds, float[] data, float volume)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = 0f;
            s.Play();
            return s;
        }

        /// <summary>Joue un son dans le monde (volume attenue par la distance a la camera).</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitchJitter = 0.1f)
        {
            if (clip == null) return;
            var s = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            s.transform.position = position;
            s.pitch = 1f + ((float)_rng.NextDouble() * 2f - 1f) * pitchJitter;
            s.PlayOneShot(clip, volume);
        }

        /// <summary>Son d'interface (sans position).</summary>
        public void PlayUi(AudioClip clip, float volume = 1f)
        {
            if (clip != null) _ui.PlayOneShot(clip, volume);
        }

        void Update()
        {
            if (_game == null) return;
            bool night = _game.Colony.IsNight;
            _crickets.volume = Mathf.MoveTowards(_crickets.volume, night ? 0.12f : 0f, Time.deltaTime * 0.05f);
            _wind.volume = Mathf.MoveTowards(_wind.volume, night ? 0.10f : 0.18f, Time.deltaTime * 0.05f);
        }
    }
}
