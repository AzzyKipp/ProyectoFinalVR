using System.Collections;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Cerradura del cajón: el cajón no se puede abrir hasta que la llave toque la cerradura.
    /// Al tocarla, la llave se inserta, gira y el cajón queda desbloqueado.
    /// </summary>
    public class KeyholeLock : MonoBehaviour
    {
        [Tooltip("Cajón bloqueado.")]
        public AxisSlideTransformer drawer;
        [Tooltip("Raíz de la llave (objeto con Grabbable).")]
        public GameObject keyRoot;
        [Tooltip("Collider de la llave usado para detectar el contacto.")]
        public Collider keyCollider;
        [Tooltip("Tamaño de la zona de detección alrededor de la cerradura (m).")]
        public Vector3 zoneSize = new Vector3(0.12f, 0.14f, 0.12f);
        [Tooltip("Dirección (local de la cerradura) hacia afuera del cajón.")]
        public Vector3 outwardLocal = Vector3.left;

        public AudioSource audioSource;
        public AudioClip unlockClip;
        public AudioClip lockedClip;
        [TextArea] public string lockedMessage = "El cajón está cerrado con llave...";

        public bool Unlocked { get; private set; }
        private float _nextLockedFeedback;

        private void Start()
        {
            if (drawer != null)
            {
                drawer.Locked = true;
                drawer.onLockedAttempt.AddListener(OnLockedAttempt);
            }
        }

        private void Update()
        {
            if (Unlocked || keyRoot == null || !keyRoot.activeInHierarchy) return;
            Bounds keyBounds = keyCollider != null ? keyCollider.bounds : ZoneUtil.GetBounds(keyRoot);
            if (ZoneUtil.Touches(transform, zoneSize, keyBounds)) StartCoroutine(UnlockRoutine());
        }

        private void OnLockedAttempt()
        {
            if (Unlocked || Time.time < _nextLockedFeedback) return;
            _nextLockedFeedback = Time.time + 2f;
            if (audioSource != null && lockedClip != null) audioSource.PlayOneShot(lockedClip);
            if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.ShowMessage(lockedMessage, 2.5f);
        }

        private IEnumerator UnlockRoutine()
        {
            Unlocked = true;

            // Copia solo visual de la llave (sin física ni interacción) que se inserta en la cerradura.
            // La llave real se desactiva: así la mano/control la suelta y su Rigidbody no puede interferir.
            var visual = new GameObject("Key (insertada)").transform;
            visual.SetParent(transform, false);
            visual.SetPositionAndRotation(keyRoot.transform.position, keyRoot.transform.rotation);
            foreach (var mr in keyRoot.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var piece = new GameObject(mr.name);
                piece.transform.SetParent(visual, false);
                piece.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
                piece.transform.localScale = mr.transform.lossyScale;
                piece.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                piece.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
            }
            keyRoot.SetActive(false);

            if (audioSource != null && unlockClip != null) audioSource.PlayOneShot(unlockClip);

            Vector3 outward = transform.TransformDirection(outwardLocal).normalized;
            Vector3 startPos = visual.position;
            Quaternion startRot = visual.rotation;
            Vector3 endScale = Vector3.one * 0.7f;
            Vector3 endPos = transform.position + outward * 0.05f;
            Quaternion endRot = Quaternion.LookRotation(outward, Vector3.up);

            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                visual.position = Vector3.Lerp(startPos, endPos, e);
                visual.rotation = Quaternion.Slerp(startRot, endRot, e);
                visual.localScale = Vector3.Lerp(Vector3.one, endScale, e);
                yield return null;
            }
            visual.SetPositionAndRotation(endPos, endRot);
            visual.localScale = endScale;

            // Giro de la llave
            Quaternion turned = endRot * Quaternion.AngleAxis(90f, Vector3.forward);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                visual.rotation = Quaternion.Slerp(endRot, turned, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            visual.rotation = turned;

            if (drawer != null) drawer.Locked = false;
            if (EscapeGameManager.Instance != null)
                EscapeGameManager.Instance.ShowMessage("¡Clic! El cajón se ha desbloqueado.", 2.5f, EscapeGameManager.Instance.goodColor);
        }

        private void OnDrawGizmosSelected() => ZoneUtil.DrawZoneGizmo(transform, zoneSize, Color.yellow);
    }
}
