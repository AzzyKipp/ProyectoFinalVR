using System.Collections;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Radio con dial. Cada vez que el dial cambia de nivel (al girar la perilla) suena estática y luego
    /// SOLO la pista que corresponde al momento del juego:
    ///   1. Antes de agarrar el cuadro del sol ........ Audio_1 ("Detrás del sol se esconde la clave del mañana")
    ///   2. Con la llave, antes de abrir el cajón ...... Audio_2 ("Abrir la mente para abrir el corazón...")
    ///   3. Cajón abierto, antes de leer el libro ...... Audio_3 ("El libro mágico debe ser leído por el ojo...")
    ///   Después de que el ojo lee el libro ya no hay pistas: solo estática.
    /// Si el jugador avanza mientras suena una pista vieja, esa pista se apaga.
    /// Girar el dial de vuelta a 0 apaga la radio.
    /// </summary>
    public class RadioController : MonoBehaviour
    {
        public AxisRotateTransformer dial;
        public AudioSource voiceSource;
        public AudioSource fxSource;

        [Tooltip("Audio_1, Audio_2, Audio_3 (una pista por etapa del juego).")]
        public AudioClip[] clueClips = new AudioClip[3];
        public AudioClip tuningClip;
        public AudioClip clickClip;

        [Header("Progreso del juego (se buscan solos si están vacíos)")]
        public SunPaintingTrap sunPainting;
        public KeyholeLock keyhole;
        public EyeBookReader eyeReader;

        [Tooltip("Ángulo (absoluto, en grados) a partir del cual se entra a cada nivel del dial.")]
        public float[] levelAngles = { 25f, 110f, 200f };
        public float hysteresis = 8f;
        [Tooltip("Segundos de estática antes de cada pista.")]
        public float tuningSeconds = 0.8f;
        [Tooltip("Segundos de estática cuando ya no quedan pistas.")]
        public float noSignalSeconds = 1.5f;
        [Tooltip("Segundos para apagar una pista que ya no corresponde al momento del juego.")]
        public float staleFadeSeconds = 0.6f;

        /// <summary>Nivel actual del dial (0 = apagada).</summary>
        public int CurrentLevel { get; private set; }

        /// <summary>Índice de la pista que corresponde al momento del juego (-1 = ya no hay pistas).</summary>
        public int CurrentClueIndex
        {
            get
            {
                if (sunPainting != null && !sunPainting.Triggered) return 0;
                if (keyhole != null && !keyhole.Unlocked) return 1;
                if (eyeReader != null && !eyeReader.Triggered) return 2;
                return -1;
            }
        }

        private int _token;
        private bool _fading;
        private float _baseVolume = 1f;

        private void Awake()
        {
            if (sunPainting == null) sunPainting = FindAnyObjectByType<SunPaintingTrap>(FindObjectsInactive.Include);
            if (keyhole == null) keyhole = FindAnyObjectByType<KeyholeLock>(FindObjectsInactive.Include);
            if (eyeReader == null) eyeReader = FindAnyObjectByType<EyeBookReader>(FindObjectsInactive.Include);
            if (voiceSource != null) _baseVolume = voiceSource.volume;
        }

        private void Update()
        {
            if (dial == null) return;
            float a = Mathf.Abs(dial.Angle);
            int level = CurrentLevel;
            while (level < levelAngles.Length && a >= levelAngles[level]) level++;
            while (level > 0 && a < levelAngles[level - 1] - hysteresis) level--;
            if (level != CurrentLevel)
            {
                CurrentLevel = level;
                OnLevelChanged();
            }

            // El jugador avanzó mientras sonaba una pista vieja: se apaga.
            if (!_fading && voiceSource != null && voiceSource.isPlaying && voiceSource.clip != CurrentClue())
            {
                _token++;
                StopAllCoroutines();
                StartCoroutine(FadeOutVoice());
            }
        }

        private AudioClip CurrentClue()
        {
            int index = CurrentClueIndex;
            return index >= 0 && clueClips != null && index < clueClips.Length ? clueClips[index] : null;
        }

        private void OnLevelChanged()
        {
            if (fxSource != null && clickClip != null) fxSource.PlayOneShot(clickClip);

            if (CurrentLevel == 0)
            {
                // Radio apagada
                StopAll();
                return;
            }

            // Si ya está sonando la pista correcta, se deja seguir (no se reinicia a cada clic del dial).
            AudioClip clue = CurrentClue();
            if (!_fading && clue != null && voiceSource != null && voiceSource.isPlaying && voiceSource.clip == clue) return;

            StopAll();
            StartCoroutine(TuneIn(_token));
        }

        private void StopAll()
        {
            _token++;
            StopAllCoroutines();
            _fading = false;
            if (voiceSource != null)
            {
                voiceSource.Stop();
                voiceSource.volume = _baseVolume;
            }
            if (fxSource != null && fxSource.clip == tuningClip) fxSource.Stop();
        }

        private IEnumerator TuneIn(int token)
        {
            AudioClip clue = CurrentClue();
            float staticTime = clue != null ? tuningSeconds : noSignalSeconds;
            if (fxSource != null && tuningClip != null)
            {
                fxSource.clip = tuningClip;
                fxSource.loop = clue == null;
                fxSource.Play();
                yield return new WaitForSeconds(clue != null ? Mathf.Min(staticTime, tuningClip.length) : staticTime);
                if (token != _token) yield break;
                fxSource.Stop();
                fxSource.loop = false;
            }

            // Se vuelve a consultar por si el jugador avanzó durante la estática.
            clue = CurrentClue();
            if (clue != null && voiceSource != null)
            {
                voiceSource.volume = _baseVolume;
                voiceSource.clip = clue;
                voiceSource.Play();
            }
        }

        private IEnumerator FadeOutVoice()
        {
            _fading = true;
            float from = voiceSource.volume;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, staleFadeSeconds))
            {
                voiceSource.volume = Mathf.Lerp(from, 0f, t);
                yield return null;
            }
            voiceSource.Stop();
            voiceSource.volume = _baseVolume;
            _fading = false;
        }
    }
}
