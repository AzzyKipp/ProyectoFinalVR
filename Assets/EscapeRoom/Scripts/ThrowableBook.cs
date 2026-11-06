using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Marca un libro que se le puede tirar a la bruja.
    /// </summary>
    public class ThrowableBook : MonoBehaviour
    {
        [Tooltip("Tiempo mínimo entre dos golpes del mismo libro.")]
        public float hitCooldown = 1f;
        private float _lastHit = -10f;

        public bool TryRegisterHit()
        {
            if (Time.time - _lastHit < hitCooldown) return false;
            _lastHit = Time.time;
            return true;
        }
    }
}
