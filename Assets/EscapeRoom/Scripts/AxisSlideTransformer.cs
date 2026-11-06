using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace EscapeRoom
{
    /// <summary>
    /// Transformer de Meta Interaction SDK que solo permite deslizar el objeto a lo largo de un eje,
    /// entre una distancia mínima y máxima (cajones, palancas deslizantes).
    /// Asignar en Grabbable → One Grab Transformer.
    /// </summary>
    public class AxisSlideTransformer : MonoBehaviour, ITransformer
    {
        [Tooltip("Objeto que se mueve (por defecto, el target del Grabbable).")]
        public Transform target;

        [Tooltip("Transform cuyo espacio local define el eje. Si es nulo se usa el target.")]
        public Transform axisReference;

        [Tooltip("Dirección de apertura/deslizamiento en el espacio local de Axis Reference.")]
        public Vector3 localAxis = Vector3.left;

        public float minDistance = 0f;
        public float maxDistance = 0.35f;

        [SerializeField, Tooltip("Si está bloqueado solo vibra un poco al intentar moverlo.")]
        private bool _locked;

        [Tooltip("Distancia que hay que tirar estando bloqueado para disparar On Locked Attempt.")]
        public float lockedFeedbackDistance = 0.02f;

        [Tooltip("Pequeño juego (m) que se permite estando bloqueado, para que se sienta trabado.")]
        public float lockedRattle = 0.006f;

        public UnityEvent onLockedAttempt = new UnityEvent();

        public bool Locked { get => _locked; set => _locked = value; }
        public float Distance { get; private set; }
        public float Normalized => Mathf.Approximately(maxDistance, minDistance)
            ? 0f : Mathf.InverseLerp(minDistance, maxDistance, Distance);
        public bool IsGrabbed { get; private set; }

        private IGrabbable _grabbable;
        private bool _initialized;
        private Vector3 _originLocal;   // posición del target (distancia 0) en espacio del padre
        private Vector3 _axisParent;    // eje en espacio del padre (solo rotación)
        private Vector3 _grabStart;
        private float _distanceAtGrab;
        private bool _lockedNotified;

        private void Awake() => Init();

        private void Init()
        {
            if (_initialized) return;
            if (target == null) target = transform;
            Transform reference = axisReference != null ? axisReference : target;
            Vector3 worldAxis = reference.TransformDirection(localAxis).normalized;
            Transform parent = target.parent;
            _axisParent = parent != null ? Quaternion.Inverse(parent.rotation) * worldAxis : worldAxis;
            _originLocal = target.localPosition;
            Distance = 0f;
            _initialized = true;
        }

        private Vector3 WorldAxis
        {
            get
            {
                Transform parent = target.parent;
                return parent != null ? parent.rotation * _axisParent : _axisParent;
            }
        }

        private Vector3 WorldOrigin
        {
            get
            {
                Transform parent = target.parent;
                return parent != null ? parent.TransformPoint(_originLocal) : _originLocal;
            }
        }

        public void SetDistance(float distance)
        {
            Init();
            float min = minDistance;
            float max = _locked ? Mathf.Min(maxDistance, minDistance + lockedRattle) : maxDistance;
            Distance = Mathf.Clamp(distance, min, max);
            target.position = WorldOrigin + WorldAxis * Distance;
        }

        #region ITransformer
        public void Initialize(IGrabbable grabbable)
        {
            _grabbable = grabbable;
            if (target == null) target = grabbable.Transform;
            Init();
        }

        public void BeginTransform()
        {
            IsGrabbed = true;
            _grabStart = _grabbable.GrabPoints[0].position;
            _distanceAtGrab = Distance;
            _lockedNotified = false;
        }

        public void UpdateTransform()
        {
            Vector3 grab = _grabbable.GrabPoints[0].position;
            float delta = Vector3.Dot(grab - _grabStart, WorldAxis);
            if (_locked && !_lockedNotified && Mathf.Abs(delta) > lockedFeedbackDistance)
            {
                _lockedNotified = true;
                onLockedAttempt.Invoke();
            }
            SetDistance(_distanceAtGrab + delta);
        }

        public void EndTransform()
        {
            IsGrabbed = false;
            if (_locked) SetDistance(minDistance);
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            Transform t = target != null ? target : transform;
            Transform reference = axisReference != null ? axisReference : t;
            Vector3 axis = reference.TransformDirection(localAxis).normalized;
            Vector3 from = Application.isPlaying && _initialized ? WorldOrigin : t.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(from + axis * minDistance, from + axis * maxDistance);
            Gizmos.DrawSphere(from + axis * maxDistance, 0.01f);
        }
    }
}
