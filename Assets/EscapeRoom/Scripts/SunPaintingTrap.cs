using System.Collections;
using Oculus.Interaction;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Cuadro morado del sol: al agarrarlo se suelta, cae al piso, desaparece en una nube de humo
    /// y en su lugar aparece la llave (ilusión de que el cuadro se convirtió en la llave).
    /// </summary>
    public class SunPaintingTrap : MonoBehaviour
    {
        public Grabbable grabbable;
        public Rigidbody body;
        [Tooltip("Llave (desactivada al inicio).")]
        public GameObject key;
        [Tooltip("Altura del piso (m).")]
        public float floorY = 0f;
        [Tooltip("Empujón para despegar el cuadro de la pared (dirección local +Z del cuadro).")]
        public float pushAwayFromWall = 0.6f;
        public float maxFallTime = 2.5f;

        public AudioSource audioSource;
        public AudioClip fallClip;
        public AudioClip poofClip;
        public AudioClip keyClip;

        private bool _triggered;

        private void OnEnable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }

        private void OnDisable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            if (_triggered || evt.Type != PointerEventType.Select) return;
            _triggered = true;
            StartCoroutine(FallAndTransform());
        }

        private IEnumerator FallAndTransform()
        {
            yield return null;
            // Obliga a soltar el cuadro.
            GrabUtil.SetInteractablesEnabled(gameObject, false);
            float wait = 0f;
            while (body != null && body.IsLocked() && wait < 1f)
            {
                wait += Time.deltaTime;
                yield return null;
            }

            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.AddForce(transform.forward * pushAwayFromWall, ForceMode.VelocityChange);
                body.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            }
            if (audioSource != null && fallClip != null) audioSource.PlayOneShot(fallClip);

            float t = 0f;
            while (t < maxFallTime)
            {
                t += Time.deltaTime;
                Bounds b = ZoneUtil.GetBounds(gameObject);
                bool onFloor = b.min.y <= floorY + 0.05f;
                bool settled = body == null || (t > 0.4f && body.linearVelocity.sqrMagnitude < 0.05f);
                if ((onFloor && t > 0.25f) || settled) break;
                yield return null;
            }

            Bounds bounds = ZoneUtil.GetBounds(gameObject);
            Vector3 landing = new Vector3(bounds.center.x, floorY, bounds.center.z);

            FxLibrary.Poof(landing + Vector3.up * 0.1f, 0.8f);
            if (audioSource != null && poofClip != null) audioSource.PlayOneShot(poofClip);

            // Encoge el cuadro rápidamente y aparece la llave.
            Vector3 startScale = transform.localScale;
            for (float s = 0f; s < 1f; s += Time.deltaTime / 0.25f)
            {
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.05f, s);
                yield return null;
            }

            if (key != null)
            {
                key.transform.position = landing + Vector3.up * 0.06f;
                key.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                key.SetActive(true);
                var keyBody = key.GetComponent<Rigidbody>();
                if (keyBody != null)
                {
                    keyBody.linearVelocity = Vector3.zero;
                    keyBody.angularVelocity = Vector3.zero;
                }
            }
            if (audioSource != null && keyClip != null) audioSource.PlayOneShot(keyClip);

            if (body != null) body.isKinematic = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            yield return new WaitForSeconds(2f); // deja terminar el audio
            gameObject.SetActive(false);
        }
    }
}
