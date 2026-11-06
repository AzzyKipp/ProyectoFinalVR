using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Efectos de partículas reutilizables (humo mágico y salpicadura del caldero).
    /// </summary>
    public class FxLibrary : MonoBehaviour
    {
        public static FxLibrary Instance { get; private set; }

        public ParticleSystem poof;
        public ParticleSystem splash;

        private void Awake() => Instance = this;

        public static void Poof(Vector3 position, float scale = 1f) => Play(Instance != null ? Instance.poof : null, position, scale);
        public static void Splash(Vector3 position, float scale = 1f) => Play(Instance != null ? Instance.splash : null, position, scale);

        private static void Play(ParticleSystem ps, Vector3 position, float scale)
        {
            if (ps == null) return;
            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * scale;
            ps.Play(true);
        }
    }
}
