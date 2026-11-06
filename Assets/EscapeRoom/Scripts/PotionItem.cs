using System.Collections;
using Oculus.Interaction;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Poción que escupe el caldero. Al agarrarla aparece un botón (poke) "Beber poción".
    /// </summary>
    public class PotionItem : MonoBehaviour
    {
        public Grabbable grabbable;
        public Rigidbody body;
        public PokeButton drinkButton;
        public Transform head;

        [Tooltip("Distancia del botón frente al jugador (m).")]
        public float buttonDistance = 0.42f;
        [Tooltip("Qué tan abajo de los ojos aparece el botón (m).")]
        public float buttonDrop = 0.22f;

        public AudioSource audioSource;
        public AudioClip grabClip;
        public AudioClip drinkClip;

        public bool Drunk { get; private set; }
        private bool _buttonShown;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised += HandlePointerEvent;
            if (drinkButton != null) drinkButton.onPressed.AddListener(Drink);
        }

        private void OnDisable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointerEvent;
            if (drinkButton != null) drinkButton.onPressed.RemoveListener(Drink);
        }

        /// <summary>Activa la poción en la posición indicada y la lanza.</summary>
        public void Launch(Vector3 position, Vector3 velocity)
        {
            transform.position = position;
            gameObject.SetActive(true);
            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = velocity;
                body.angularVelocity = Random.insideUnitSphere * 4f;
            }
        }

        private void HandlePointerEvent(PointerEvent evt)
        {
            if (evt.Type != PointerEventType.Select || Drunk) return;
            if (!_buttonShown) ShowButton();
        }

        private void ShowButton()
        {
            if (drinkButton == null) return;
            _buttonShown = true;
            if (head == null && Camera.main != null) head = Camera.main.transform;
            if (audioSource != null && grabClip != null) audioSource.PlayOneShot(grabClip);

            Vector3 forward = head != null ? ZoneUtil.FlatForward(head) : Vector3.forward;
            Vector3 origin = head != null ? head.position : transform.position;
            Vector3 pos = origin + forward * buttonDistance + Vector3.down * buttonDrop;
            // +Z del botón apunta hacia adentro (lejos del jugador), inclinado para mirar a los ojos.
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(25f, 0f, 0f);
            drinkButton.transform.SetPositionAndRotation(pos, rot);
            drinkButton.gameObject.SetActive(true);
            FxLibrary.Poof(pos, 0.3f);
        }

        public void Drink()
        {
            if (Drunk) return;
            Drunk = true;
            if (audioSource != null && drinkClip != null) audioSource.PlayOneShot(drinkClip);
            if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.DrinkPotion();
            StartCoroutine(DisappearRoutine());
        }

        private IEnumerator DisappearRoutine()
        {
            GrabUtil.SetInteractablesEnabled(gameObject, false);
            yield return new WaitForSeconds(0.3f);
            if (drinkButton != null) drinkButton.gameObject.SetActive(false);
            Vector3 start = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                transform.localScale = Vector3.Lerp(start, start * 0.01f, t);
                yield return null;
            }
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            yield return new WaitForSeconds(2f);
            gameObject.SetActive(false);
        }
    }
}
