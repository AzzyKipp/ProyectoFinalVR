using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Puerta de salida: estática y bloqueada hasta que el jugador bebe la poción.
    /// Luego se puede abrir (bisagra con límites) y al abrirla el jugador escapa.
    /// </summary>
    public class ExitDoorController : MonoBehaviour
    {
        public AxisRotateTransformer hinge;
        [Tooltip("Ángulo de apertura a partir del cual se considera que el jugador escapó.")]
        public float escapeAngle = 50f;
        [Tooltip("Al desbloquearse, la puerta se entreabre sola estos grados.")]
        public float crackOpenAngle = 8f;

        public AudioSource audioSource;
        public AudioClip lockedClip;
        public AudioClip unlockClip;
        [TextArea] public string lockedMessage = "La puerta está sellada con magia...\nQuizás una poción ayude.";

        public bool Unlocked { get; private set; }
        private bool _escaped;
        private float _nextLockedFeedback;
        private float _crackTarget = -1f;

        private void Start()
        {
            if (hinge != null)
            {
                hinge.Locked = true;
                hinge.onLockedAttempt.AddListener(OnLockedAttempt);
            }
        }

        public void Unlock()
        {
            if (Unlocked) return;
            Unlocked = true;
            if (hinge != null)
            {
                hinge.Locked = false;
                _crackTarget = Mathf.Min(hinge.maxAngle, hinge.Angle + crackOpenAngle);
            }
            if (audioSource != null && unlockClip != null) audioSource.PlayOneShot(unlockClip);
        }

        private void Update()
        {
            if (!Unlocked || hinge == null) return;

            if (_crackTarget >= 0f && !hinge.IsGrabbed)
            {
                float a = Mathf.MoveTowards(hinge.Angle, _crackTarget, 15f * Time.deltaTime);
                hinge.SetAngle(a);
                if (Mathf.Approximately(a, _crackTarget)) _crackTarget = -1f;
            }
            else if (hinge.IsGrabbed)
            {
                _crackTarget = -1f;
            }

            if (!_escaped && hinge.Angle >= escapeAngle)
            {
                _escaped = true;
                if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.PlayerEscaped();
            }
        }

        private void OnLockedAttempt()
        {
            if (Unlocked || Time.time < _nextLockedFeedback) return;
            _nextLockedFeedback = Time.time + 2.5f;
            if (audioSource != null && lockedClip != null) audioSource.PlayOneShot(lockedClip);
            if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.ShowMessage(lockedMessage, 3f);
        }
    }
}
