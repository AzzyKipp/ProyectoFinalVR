using System.Collections;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// "El libro mágico debe ser leído por el ojo que todo lo ve": cuando el libro de recetas
    /// permanece frente a la chimenea del ojo, la comida aparece y cae en la boca de la chimenea.
    /// </summary>
    public class EyeBookReader : MonoBehaviour
    {
        [Tooltip("Collider(s) del libro de recetas.")]
        public Collider[] bookColliders;
        [Tooltip("Tamaño de la zona frente al ojo (m). La zona está centrada en este transform.")]
        public Vector3 zoneSize = new Vector3(1.0f, 1.6f, 1.6f);
        [Tooltip("Segundos que el libro debe permanecer frente al ojo.")]
        public float requiredTime = 1.0f;

        [Tooltip("Comida que aparece (desactivada al inicio).")]
        public GameObject[] foodToActivate;
        public float delayBetweenFood = 0.25f;

        public Light eyeLight;
        public AudioSource audioSource;
        public AudioClip readClip;
        public AudioClip dropClip;

        public bool Triggered { get; private set; }
        private float _timer;

        private void Update()
        {
            if (Triggered) return;
            bool inside = false;
            foreach (var c in bookColliders)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy && ZoneUtil.Touches(transform, zoneSize, c.bounds))
                {
                    inside = true;
                    break;
                }
            }
            _timer = inside ? _timer + Time.deltaTime : Mathf.Max(0f, _timer - Time.deltaTime * 2f);
            if (eyeLight != null) eyeLight.intensity = Mathf.Lerp(0f, 2.5f, _timer / requiredTime);
            if (_timer >= requiredTime) StartCoroutine(TriggerRoutine());
        }

        private IEnumerator TriggerRoutine()
        {
            Triggered = true;
            if (audioSource != null && readClip != null) audioSource.PlayOneShot(readClip);
            if (eyeLight != null)
            {
                eyeLight.enabled = true;
                eyeLight.intensity = 4f;
            }
            if (EscapeGameManager.Instance != null)
                EscapeGameManager.Instance.ShowMessage("El ojo que todo lo ve ha leído el libro...", 3f, EscapeGameManager.Instance.goodColor);

            yield return new WaitForSeconds(0.6f);
            foreach (var food in foodToActivate)
            {
                if (food == null) continue;
                food.SetActive(true);
                var bodies = food.GetComponentsInChildren<Rigidbody>();
                foreach (var b in bodies)
                {
                    b.isKinematic = false;
                    b.useGravity = true;
                    b.WakeUp();
                }
                if (audioSource != null && dropClip != null) audioSource.PlayOneShot(dropClip, 0.6f);
                yield return new WaitForSeconds(delayBetweenFood);
            }

            if (eyeLight != null)
            {
                for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
                {
                    eyeLight.intensity = Mathf.Lerp(4f, 0f, t);
                    yield return null;
                }
                eyeLight.intensity = 0f;
            }
        }

        private void OnDrawGizmosSelected() => ZoneUtil.DrawZoneGizmo(transform, zoneSize, new Color(1f, 0.5f, 0f));
    }
}
