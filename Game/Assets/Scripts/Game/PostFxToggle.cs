using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AfterCity.Game
{
    /// <summary>
    /// Effets visuels facon « ReShade » : occlusion ambiante (SSAO) + volume de post-traitement
    /// (ACES, couleurs chaudes un peu desaturees, ombres froides, bloom, vignette, grain).
    /// F2 les coupe ou les remet pour comparer.
    /// </summary>
    public sealed class PostFxToggle : MonoBehaviour
    {
        public Volume volume;
        public ScriptableRendererFeature ssao;
        public UniversalAdditionalCameraData cameraData;

        public bool Enabled { get; private set; } = true;

        void Start() => Apply(true);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2)) Apply(!Enabled);
        }

        public void Apply(bool on)
        {
            Enabled = on;
            if (volume != null) volume.enabled = on;
            if (ssao != null) ssao.SetActive(on);
            if (cameraData != null) cameraData.renderPostProcessing = on;
        }

        void OnDestroy()
        {
            // La fonction SSAO est un asset partage : on la laisse active pour l'editeur
            if (ssao != null) ssao.SetActive(true);
        }
    }
}
