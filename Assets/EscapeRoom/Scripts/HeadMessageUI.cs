using TMPro;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Mensaje flotante (canvas en World Space) que aparece frente al jugador y lo sigue suavemente.
    /// </summary>
    public class HeadMessageUI : MonoBehaviour
    {
        public Transform head;
        public CanvasGroup group;
        public TMP_Text label;

        [Tooltip("Distancia frente a la cabeza (m).")]
        public float distance = 1.1f;
        public float verticalOffset = -0.05f;
        public float followSharpness = 3f;
        public float fadeSpeed = 4f;

        private float _hideTime;
        private bool _visible;
        private Vector3 _targetPosition;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (group != null) group.alpha = 0f;
        }

        public void Show(string text, float duration, Color color)
        {
            if (label != null)
            {
                label.text = text;
                label.color = color;
            }
            _hideTime = Time.time + duration;
            _visible = true;
            if (group != null && group.alpha < 0.05f) Snap();
        }

        public void Hide() => _visible = false;

        private void Snap()
        {
            if (head == null) return;
            ComputeTarget();
            transform.position = _targetPosition;
            FaceHead();
        }

        private void ComputeTarget()
        {
            _targetPosition = head.position + ZoneUtil.FlatForward(head) * distance + Vector3.up * verticalOffset;
        }

        private void FaceHead()
        {
            Vector3 dir = transform.position - head.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void LateUpdate()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (_visible && Time.time >= _hideTime) _visible = false;
            if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, _visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
                if (!_visible && group.alpha <= 0f) return;
            }
            if (head == null) return;
            ComputeTarget();
            transform.position = Vector3.Lerp(transform.position, _targetPosition, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            FaceHead();
        }
    }
}
