using System.Collections;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace EscapeRoom
{
    /// <summary>
    /// Botón que se presiona con poke (dedo de la mano o del control).
    /// Usa el PokeInteractable de Meta y, como respaldo, detecta la punta de cualquier PokeInteractor
    /// que atraviese la cara del botón.
    /// </summary>
    public class PokeButton : MonoBehaviour
    {
        public PokeInteractable pokeInteractable;

        [Tooltip("Marco del botón: origen en la cara del botón, +Z apunta hacia adentro (dirección del empuje). Escala 1.")]
        public Transform pressFrame;
        [Tooltip("Mitad del ancho/alto del área presionable (m).")]
        public Vector2 halfSize = new Vector2(0.06f, 0.06f);
        [Tooltip("Profundidad (m) que debe atravesar la punta del dedo para contar como presionado.")]
        public float pressDepth = 0.012f;
        public bool proximityFallback = true;

        [Header("Visual")]
        public Transform pressVisual;
        public float pressTravel = 0.012f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip pressClip;

        public float cooldown = 0.8f;
        public UnityEvent onPressed = new UnityEvent();

        private PokeInteractor[] _pokers = new PokeInteractor[0];
        private float _nextRefresh;
        private float _lastPress = -10f;
        private bool _armed = true;
        private bool _animating;

        private void OnEnable()
        {
            if (pokeInteractable != null) pokeInteractable.WhenPointerEventRaised += HandlePointerEvent;
            _armed = true;
        }

        private void OnDisable()
        {
            if (pokeInteractable != null) pokeInteractable.WhenPointerEventRaised -= HandlePointerEvent;
            _animating = false;
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select) Press();
        }

        private void Update()
        {
            if (!proximityFallback || pressFrame == null) return;

            if (Time.time >= _nextRefresh)
            {
                _pokers = FindObjectsByType<PokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                _nextRefresh = Time.time + 2f;
            }

            bool inside = false, near = false;
            foreach (var poker in _pokers)
            {
                if (poker == null || !poker.isActiveAndEnabled) continue;
                Vector3 local = Quaternion.Inverse(pressFrame.rotation) * (poker.Origin - pressFrame.position);
                if (Mathf.Abs(local.x) > halfSize.x || Mathf.Abs(local.y) > halfSize.y) continue;
                if (local.z > -0.05f && local.z < 0.08f) near = true;
                if (local.z >= pressDepth && local.z < pressDepth + 0.06f) inside = true;
            }

            if (inside && _armed)
            {
                _armed = false;
                Press();
            }
            else if (!near)
            {
                _armed = true;
            }
        }

        public void Press()
        {
            if (!isActiveAndEnabled || Time.time - _lastPress < cooldown) return;
            _lastPress = Time.time;
            if (audioSource != null && pressClip != null) audioSource.PlayOneShot(pressClip);
            if (pressVisual != null && !_animating) StartCoroutine(AnimatePress());
            onPressed.Invoke();
        }

        private IEnumerator AnimatePress()
        {
            _animating = true;
            Vector3 start = pressVisual.localPosition;
            Vector3 worldOffset = (pressFrame != null ? pressFrame.forward : -pressVisual.up) * pressTravel;
            Vector3 pressed = start + (pressVisual.parent != null
                ? pressVisual.parent.InverseTransformVector(worldOffset)
                : worldOffset);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.08f)
            {
                pressVisual.localPosition = Vector3.Lerp(start, pressed, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.12f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                pressVisual.localPosition = Vector3.Lerp(pressed, start, t);
                yield return null;
            }
            pressVisual.localPosition = start;
            _animating = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (pressFrame == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(pressFrame.position, pressFrame.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0, 0, pressDepth * 0.5f), new Vector3(halfSize.x * 2f, halfSize.y * 2f, pressDepth));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
