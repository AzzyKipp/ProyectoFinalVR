using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Caldero: hay que echar todos los ingredientes, cerrar la tapa, mover la palanca de fuerza,
    /// girar la palanca de temperatura y presionar BREW. Entonces la tapa se abre y escupe la poción.
    /// </summary>
    public class CauldronController : MonoBehaviour
    {
        [Header("Ingredientes")]
        public GameObject[] ingredients;
        [Tooltip("Zona dentro del caldero (centrada en este transform si es nulo).")]
        public Transform potZone;
        [Tooltip("Tamaño de la zona: debe llegar hasta el fondo del caldero para no perder ingredientes.")]
        public Vector3 potZoneSize = new Vector3(0.7f, 0.95f, 0.75f);

        [Header("Controles")]
        public AxisRotateTransformer lid;
        [Tooltip("Ángulo máximo de la tapa para considerarla cerrada.")]
        public float lidClosedAngle = 6f;
        public float lidOpenAngle = 110f;
        public AxisSlideTransformer spinLever;
        [Tooltip("Recorrido normalizado (0-1) necesario de la palanca de fuerza.")]
        public float spinRequired = 0.6f;
        public AxisRotateTransformer temperatureLever;
        [Tooltip("Grados que hay que girar la palanca de temperatura.")]
        public float temperatureRequired = 90f;
        public PokeButton brewButton;

        [Header("Poción")]
        public PotionItem potion;
        public Transform potionSpawn;
        [Tooltip("Velocidad con la que el caldero escupe la poción (espacio mundo).")]
        public Vector3 potionLaunchVelocity = new Vector3(0f, 3.6f, -1.6f);

        [Header("Audio")]
        public AudioSource sfx;
        public AudioSource bubbleLoop;
        public AudioClip splashClip;
        public AudioClip brewClip;
        public AudioClip errorClip;
        public AudioClip readyClip;
        public AudioClip lidClip;

        public int IngredientsIn { get; private set; }
        public bool Brewed { get; private set; }
        public bool LidClosed => lid == null || lid.Angle <= lidClosedAngle;
        public bool SpinDone => spinLever == null || spinLever.Normalized >= spinRequired;
        public bool TemperatureDone => temperatureLever == null || Mathf.Abs(temperatureLever.Angle) >= temperatureRequired;

        private bool[] _consumed;

        private void Awake()
        {
            _consumed = new bool[ingredients != null ? ingredients.Length : 0];
            if (potZone == null) potZone = transform;
        }

        private void OnEnable()
        {
            if (brewButton != null) brewButton.onPressed.AddListener(TryBrew);
        }

        private void OnDisable()
        {
            if (brewButton != null) brewButton.onPressed.RemoveListener(TryBrew);
        }

        // FixedUpdate: se revisa en cada paso de física para no perder ingredientes que caen rápido.
        private void FixedUpdate()
        {
            if (ingredients == null) return;
            for (int i = 0; i < ingredients.Length; i++)
            {
                GameObject go = ingredients[i];
                if (_consumed[i] || go == null || !go.activeInHierarchy) continue;
                Vector3 center = ZoneUtil.GetBounds(go).center;
                if (ZoneUtil.Contains(potZone, potZoneSize, center)) StartCoroutine(Consume(i));
            }
        }

        private IEnumerator Consume(int index)
        {
            _consumed[index] = true;
            IngredientsIn++;
            GameObject go = ingredients[index];
            Vector3 pos = ZoneUtil.GetBounds(go).center;
            FxLibrary.Splash(pos, 0.7f);
            if (sfx != null && splashClip != null) sfx.PlayOneShot(splashClip);

            GrabUtil.SetInteractablesEnabled(go, false);
            if (EscapeGameManager.Instance != null)
                EscapeGameManager.Instance.ShowMessage($"Ingredientes en el caldero: {IngredientsIn}/{ingredients.Length}", 2f);

            // Se hunde y desaparece (se encoge cada malla alrededor de su propio pivote)
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
            }
            var renderers = go.GetComponentsInChildren<Renderer>();
            var scales = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) scales[i] = renderers[i].transform.localScale;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.4f)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].transform.localScale = Vector3.Lerp(scales[i], scales[i] * 0.01f, k);
                    renderers[i].transform.position += Vector3.down * Time.deltaTime * 0.3f;
                }
                yield return null;
            }
            go.SetActive(false);
        }

        public void TryBrew()
        {
            if (Brewed) return;
            var missing = new List<string>();
            int total = ingredients != null ? ingredients.Length : 0;
            if (IngredientsIn < total) missing.Add($"Faltan ingredientes ({IngredientsIn}/{total})");
            if (!LidClosed) missing.Add("La tapa debe estar cerrada");
            if (!SpinDone) missing.Add("Mueve la palanca de fuerza");
            if (!TemperatureDone) missing.Add("Gira la palanca de temperatura");

            if (missing.Count > 0)
            {
                if (sfx != null && errorClip != null) sfx.PlayOneShot(errorClip);
                if (EscapeGameManager.Instance != null)
                    EscapeGameManager.Instance.ShowMessage("El caldero no responde...\n" + string.Join("\n", missing), 4f);
                return;
            }
            StartCoroutine(BrewRoutine());
        }

        private IEnumerator BrewRoutine()
        {
            Brewed = true;
            if (sfx != null && brewClip != null) sfx.PlayOneShot(brewClip);
            if (bubbleLoop != null) bubbleLoop.Play();
            if (EscapeGameManager.Instance != null)
                EscapeGameManager.Instance.ShowMessage("El caldero está preparando algo...", 2.5f);

            for (int i = 0; i < 6; i++)
            {
                FxLibrary.Splash(potZone.position + Random.insideUnitSphere * 0.15f, 0.6f);
                yield return new WaitForSeconds(0.4f);
            }

            // La tapa se abre sola
            if (lid != null)
            {
                GrabUtil.SetInteractablesEnabled(lid.gameObject, false);
                yield return null;
                if (sfx != null && lidClip != null) sfx.PlayOneShot(lidClip);
                float from = lid.Angle;
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
                {
                    lid.SetAngle(Mathf.Lerp(from, lidOpenAngle, 1f - (1f - t) * (1f - t)));
                    yield return null;
                }
                lid.SetAngle(lidOpenAngle);
            }

            // ¡Escupe la poción!
            if (potion != null)
            {
                Vector3 spawn = potionSpawn != null ? potionSpawn.position : potZone.position + Vector3.up * 0.2f;
                potion.Launch(spawn, potionLaunchVelocity);
                FxLibrary.Splash(spawn, 1f);
            }
            if (sfx != null && readyClip != null) sfx.PlayOneShot(readyClip);
            if (EscapeGameManager.Instance != null)
                EscapeGameManager.Instance.ShowMessage("¡La poción está lista! Agárrala.", 3f, EscapeGameManager.Instance.goodColor);

            yield return new WaitForSeconds(4f);
            if (bubbleLoop != null) bubbleLoop.Stop();
            if (lid != null) GrabUtil.SetInteractablesEnabled(lid.gameObject, true);
        }

        private void OnDrawGizmosSelected() => ZoneUtil.DrawZoneGizmo(potZone != null ? potZone : transform, potZoneSize, Color.green);
    }
}
