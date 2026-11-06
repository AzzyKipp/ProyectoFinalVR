using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace EscapeRoom
{
    /// <summary>
    /// La bruja: aparece cuando se acaba el tiempo, persigue lentamente al jugador caminando,
    /// lo ataca (4 golpes = derrota) y se ríe de vez en cuando. Muere con 3 libros lanzados.
    /// </summary>
    public class WitchController : MonoBehaviour
    {
        [Header("Referencias")]
        public Transform player;
        public Animator animator;
        public NavMeshAgent agent;
        public Collider bodyCollider;
        public Transform[] spawnPoints;

        [Header("Movimiento")]
        public float walkSpeed = 0.6f;
        public float turnSpeed = 180f;

        [Header("Ataque")]
        [Tooltip("Distancia horizontal a la que empieza a atacar.")]
        public float attackRange = 1.25f;
        [Tooltip("Distancia máxima a la que el golpe conecta.")]
        public float hitRange = 1.8f;
        public float attackCooldown = 2.5f;
        [Tooltip("Momento del golpe dentro de la animación de ataque.")]
        public float attackHitDelay = 0.55f;
        public float attackDuration = 1.15f;

        [Header("Libros")]
        public int booksToKill = 3;
        [Tooltip("Velocidad mínima del libro para contar como golpe.")]
        public float minBookSpeed = 1.0f;

        [Header("Audio")]
        public AudioSource voice;
        public AudioSource sfx;
        public AudioClip laughClip;
        public Vector2 laughInterval = new Vector2(5f, 10f);
        public AudioClip bookHitClip;
        public AudioClip attackClip;
        public AudioClip appearClip;

        [Header("Estados del Animator")]
        public string idleState = "Idle";
        public string walkState = "Walk";
        public string attackState = "Attack";
        public string deathState = "Death";

        public int BookHits { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsDead { get; private set; }

        private float _nextAttack;
        private float _nextLaugh;
        private float _nextRepath;
        private bool _attacking;
        private string _currentState;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (player == null && Camera.main != null) player = Camera.main.transform;
        }

        /// <summary>La bruja aparece en el punto de aparición más lejano al jugador.</summary>
        public void Appear()
        {
            if (player == null && Camera.main != null) player = Camera.main.transform;
            Vector3 spawn = transform.position;
            if (spawnPoints != null && spawnPoints.Length > 0 && player != null)
            {
                float best = -1f;
                foreach (var sp in spawnPoints)
                {
                    if (sp == null) continue;
                    float d = FlatDistance(sp.position, player.position);
                    if (d > best) { best = d; spawn = sp.position; }
                }
            }

            gameObject.SetActive(true);
            if (agent != null)
            {
                agent.enabled = true;
                agent.speed = walkSpeed;
                agent.angularSpeed = turnSpeed;
                agent.stoppingDistance = attackRange * 0.8f;
                agent.Warp(spawn);
            }
            else
            {
                transform.position = spawn;
            }
            if (player != null)
            {
                Vector3 look = player.position - spawn;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            IsActive = true;
            IsDead = false;
            BookHits = 0;
            _nextAttack = Time.time + 2f;
            _nextLaugh = Time.time + Random.Range(laughInterval.x, laughInterval.y);
            if (bodyCollider != null) bodyCollider.enabled = true;

            FxLibrary.Poof(spawn + Vector3.up * 1f, 2f);
            if (sfx != null && appearClip != null) sfx.PlayOneShot(appearClip);
            Laugh();
            Play(walkState, 0.2f);
        }

        private void Update()
        {
            if (!IsActive || IsDead || player == null) return;

            if (Time.time >= _nextLaugh)
            {
                Laugh();
                _nextLaugh = Time.time + Random.Range(laughInterval.x, laughInterval.y);
            }

            if (_attacking) return;

            Vector3 target = new Vector3(player.position.x, transform.position.y, player.position.z);
            float distance = FlatDistance(transform.position, player.position);

            if (distance > attackRange)
            {
                MoveTowards(target);
                Play(walkState, 0.2f);
            }
            else
            {
                StopMoving();
                FaceTowards(player.position, turnSpeed);
                if (Time.time >= _nextAttack) StartCoroutine(AttackRoutine());
                else Play(idleState, 0.2f);
            }
        }

        private void MoveTowards(Vector3 target)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                if (Time.time >= _nextRepath)
                {
                    _nextRepath = Time.time + 0.25f;
                    agent.SetDestination(target);
                }
                // Si el NavMesh ya no la acerca más (llegó al final del camino), avanza directo hacia el jugador.
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    Vector3 dir = target - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 1e-4f)
                    {
                        agent.Move(dir.normalized * walkSpeed * Time.deltaTime);
                        FaceTowards(target, turnSpeed);
                    }
                }
            }
            else
            {
                FaceTowards(target, turnSpeed);
                transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
            }
        }

        private void StopMoving()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }

        private IEnumerator AttackRoutine()
        {
            _attacking = true;
            StopMoving();
            Play(attackState, 0.1f, true);
            if (sfx != null && attackClip != null) sfx.PlayOneShot(attackClip);
            yield return new WaitForSeconds(attackHitDelay);
            if (!IsDead && IsActive && player != null && FlatDistance(transform.position, player.position) <= hitRange)
            {
                if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.PlayerHit();
            }
            yield return new WaitForSeconds(Mathf.Max(0f, attackDuration - attackHitDelay));
            _nextAttack = Time.time + attackCooldown;
            _attacking = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsActive || IsDead) return;
            Rigidbody rb = collision.rigidbody;
            if (rb == null) return;
            var book = rb.GetComponentInParent<ThrowableBook>();
            if (book == null) return;
            if (collision.relativeVelocity.magnitude < minBookSpeed) return;
            if (!book.TryRegisterHit()) return;

            BookHits++;
            if (sfx != null && bookHitClip != null) sfx.PlayOneShot(bookHitClip);
            if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.WitchHitByBook(BookHits, booksToKill);
            if (BookHits >= booksToKill) Die();
        }

        private void Die()
        {
            IsDead = true;
            StopAllCoroutines();
            _attacking = false;
            StopMoving();
            if (agent != null) agent.enabled = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (voice != null) voice.Stop();
            Play(deathState, 0.15f, true);
            if (EscapeGameManager.Instance != null) EscapeGameManager.Instance.WitchDefeated();
            StartCoroutine(SinkAndHide(3.2f));
        }

        /// <summary>Desaparece en una nube de humo (por ejemplo, si el jugador bebe la poción).</summary>
        public void Vanish()
        {
            if (!gameObject.activeInHierarchy) return;
            IsActive = false;
            StopAllCoroutines();
            Laugh();
            FxLibrary.Poof(transform.position + Vector3.up, 2f);
            if (agent != null) agent.enabled = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            StartCoroutine(SinkAndHide(0.2f));
        }

        /// <summary>El jugador perdió: la bruja deja de atacar y se ríe.</summary>
        public void OnPlayerCaught()
        {
            IsActive = false;
            StopMoving();
            Play(idleState, 0.2f);
            Laugh();
        }

        private IEnumerator SinkAndHide(float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 start = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
            {
                transform.position = start + Vector3.down * (2.2f * t);
                yield return null;
            }
            FxLibrary.Poof(start + Vector3.up * 0.3f, 1.5f);
            IsActive = false;
            gameObject.SetActive(false);
        }

        private void Laugh()
        {
            if (voice != null && laughClip != null && !voice.isPlaying)
            {
                voice.clip = laughClip;
                voice.Play();
            }
        }

        private void Play(string state, float fade, bool restart = false)
        {
            if (animator == null || string.IsNullOrEmpty(state)) return;
            if (!restart && _currentState == state) return;
            _currentState = state;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        private void FaceTowards(Vector3 point, float maxDegrees)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDegrees * Time.deltaTime);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
