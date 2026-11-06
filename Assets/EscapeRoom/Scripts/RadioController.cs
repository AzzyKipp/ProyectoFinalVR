using System.Collections;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Radio con dial de 3 niveles. Cada vez que el dial entra a un nivel nuevo suena la pista
    /// correspondiente: nivel 1 = Audio_1 (enciende la radio), nivel 2 = Audio_2, nivel 3 = Audio_3.
    /// </summary>
    public class RadioController : MonoBehaviour
    {
        public AxisRotateTransformer dial;
        public AudioSource voiceSource;
        public AudioSource fxSource;

        [Tooltip("Audio_1, Audio_2, Audio_3")]
        public AudioClip[] clueClips = new AudioClip[3];
        public AudioClip tuningClip;
        public AudioClip clickClip;

        [Tooltip("Ángulo (absoluto, en grados) a partir del cual se entra a cada nivel.")]
        public float[] levelAngles = { 25f, 110f, 200f };
        public float hysteresis = 8f;
        [Tooltip("Segundos de estática antes de cada pista.")]
        public float tuningSeconds = 0.8f;

        public int CurrentLevel { get; private set; }

        private int _token;

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
        }

        private void OnLevelChanged()
        {
            _token++;
            StopAllCoroutines();
            if (voiceSource != null) voiceSource.Stop();
            if (fxSource != null)
            {
                fxSource.Stop();
                if (clickClip != null) fxSource.PlayOneShot(clickClip);
            }
            if (CurrentLevel > 0) StartCoroutine(PlayClue(CurrentLevel, _token));
        }

        private IEnumerator PlayClue(int level, int token)
        {
            if (fxSource != null && tuningClip != null)
            {
                fxSource.clip = tuningClip;
                fxSource.Play();
                yield return new WaitForSeconds(Mathf.Min(tuningSeconds, tuningClip.length));
                if (token != _token) yield break;
                fxSource.Stop();
            }
            int index = level - 1;
            if (voiceSource != null && index < clueClips.Length && clueClips[index] != null)
            {
                voiceSource.clip = clueClips[index];
                voiceSource.Play();
            }
        }
    }
}
