using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeRoom
{
    public enum GameState { Intro, Playing, WitchChase, Won, Lost }

    /// <summary>
    /// Controla el flujo del escape room: mensaje inicial, contador de 8 minutos,
    /// aparición de la bruja, poción, victoria y derrota.
    /// </summary>
    public class EscapeGameManager : MonoBehaviour
    {
        public static EscapeGameManager Instance { get; private set; }

        [Header("Tiempo")]
        [Tooltip("Duración del contador en segundos (8 minutos = 480).")]
        public float totalSeconds = 480f;

        [Header("Inicio")]
        [TextArea] public string introText = "Escapa antes que la bruja te encuentre y te cocine";
        public float introDuration = 4f;
        public Color introColor = new Color(1f, 0.85f, 0.5f);

        [Header("Referencias")]
        public HeadMessageUI hud;
        public CountdownTimerUI timerUI;
        public WitchController witch;
        public ExitDoorController exitDoor;
        public Transform playerHead;

        [Header("Jugador")]
        [Tooltip("Golpes de la bruja para perder.")]
        public int hitsToLose = 4;
        [Tooltip("Segundos antes de reiniciar la escena al perder.")]
        public float restartDelay = 7f;

        [Header("Sonidos")]
        public AudioSource sfx;
        public AudioClip hurtClip;
        public AudioClip timeUpClip;
        public AudioClip winClip;
        public AudioClip loseClip;

        [Header("Colores")]
        public Color infoColor = new Color(1f, 0.92f, 0.75f);
        public Color goodColor = new Color(0.55f, 1f, 0.55f);
        public Color badColor = new Color(1f, 0.35f, 0.3f);

        public GameState State { get; private set; } = GameState.Intro;
        public float TimeRemaining { get; private set; }
        public bool PotionDrunk { get; private set; }
        public int PlayerHits { get; private set; }

        private bool _timeUpTriggered;

        private void Awake()
        {
            Instance = this;
            TimeRemaining = totalSeconds;
            if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
        }

        private IEnumerator Start()
        {
            if (timerUI != null) timerUI.SetTime(TimeRemaining);
            // Pequeña espera para que el casco reporte la pose de la cabeza.
            yield return new WaitForSeconds(0.4f);
            ShowMessage(introText, introDuration, introColor);
            yield return new WaitForSeconds(introDuration);
            if (State == GameState.Intro) State = GameState.Playing;
        }

        private void Update()
        {
            if (PotionDrunk || _timeUpTriggered) return;
            if (State != GameState.Playing) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                OnTimeUp();
            }
            if (timerUI != null) timerUI.SetTime(TimeRemaining);
        }

        public void ShowMessage(string text, float duration, Color color)
        {
            if (hud != null) hud.Show(text, duration, color);
            else Debug.Log($"[EscapeRoom] {text}");
        }

        public void ShowMessage(string text, float duration = 3f) => ShowMessage(text, duration, infoColor);

        private void OnTimeUp()
        {
            _timeUpTriggered = true;
            if (timerUI != null) timerUI.SetTime(0f);
            State = GameState.WitchChase;
            PlayClip(timeUpClip);
            ShowMessage("¡Se acabó el tiempo!\nLa bruja ha vuelto a casa...", 4f, badColor);
            if (witch != null) witch.Appear();
        }

        public void PlayerHit()
        {
            if (State != GameState.WitchChase) return;
            PlayerHits++;
            PlayClip(hurtClip);
            if (PlayerHits >= hitsToLose)
            {
                Lose();
            }
            else
            {
                ShowMessage($"¡La bruja te atacó! ({PlayerHits}/{hitsToLose})\nTírale libros para defenderte", 3f, badColor);
            }
        }

        public void WitchHitByBook(int hits, int needed)
        {
            if (State != GameState.WitchChase) return;
            if (hits < needed) ShowMessage($"¡Le diste a la bruja! ({hits}/{needed})", 2f, goodColor);
        }

        public void WitchDefeated()
        {
            if (State != GameState.WitchChase) return;
            State = GameState.Playing;
            ShowMessage("¡Derrotaste a la bruja!\nTermina la poción y escapa.", 5f, goodColor);
        }

        private void Lose()
        {
            State = GameState.Lost;
            PlayClip(loseClip);
            if (witch != null) witch.OnPlayerCaught();
            ShowMessage("La bruja te atrapó...\n¡Ahora serás su cena!", restartDelay, badColor);
            StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().name
                : SceneManager.GetActiveScene().path);
        }

        public void DrinkPotion()
        {
            if (PotionDrunk || State == GameState.Lost) return;
            PotionDrunk = true;
            if (State == GameState.WitchChase && witch != null) witch.Vanish();
            State = GameState.Playing;
            if (exitDoor != null) exitDoor.Unlock();
            ShowMessage("¡Bebiste la poción!\nLa puerta se ha abierto... ¡Escapa!", 5f, goodColor);
        }

        public void PlayerEscaped()
        {
            if (State == GameState.Won || State == GameState.Lost) return;
            State = GameState.Won;
            PlayClip(winClip);
            ShowMessage("¡Escapaste de la casa de la bruja!", 10f, goodColor);
        }

        private void PlayClip(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Saltar a 10 segundos")]
        private void DebugSkipTime() => TimeRemaining = Mathf.Min(TimeRemaining, 10f);

        [ContextMenu("Debug/Beber poción")]
        private void DebugDrink() => DrinkPotion();
#endif
    }
}
