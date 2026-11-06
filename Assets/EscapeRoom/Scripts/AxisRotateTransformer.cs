using System;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace EscapeRoom
{
    /// <summary>
    /// Transformer de Meta Interaction SDK que solo permite girar el objeto alrededor de un eje
    /// que pasa por su pivote, con límites de ángulo (puertas, tapas, perillas, palancas).
    ///  - Orbit: el ángulo sigue la posición de la mano alrededor del pivote (puertas, tapas).
    ///  - Twist: el ángulo sigue el giro de la muñeca (perillas y diales pequeños).
    ///  - OrbitOrTwist: usa el que más se mueva en cada momento; sirve para perillas que se giran
    ///    con la muñeca o arrastrando la mano/el rayo alrededor del centro (útil en el simulador).
    /// </summary>
    public class AxisRotateTransformer : MonoBehaviour, ITransformer
    {
        public enum RotationMode { Orbit, Twist, OrbitOrTwist }

        [Tooltip("Objeto que gira (por defecto, el target del Grabbable). Su pivote es el centro de giro.")]
        public Transform target;

        public RotationMode mode = RotationMode.Orbit;

        [Tooltip("En OrbitOrTwist, distancia mínima de la mano al eje para usar el giro alrededor del centro (m).")]
        public float minOrbitRadius = 0.02f;

        [Tooltip("Eje de giro en el espacio local del target.")]
        public Vector3 localAxis = Vector3.up;

        [Tooltip("Ángulo que representa la pose actual del objeto en la escena.")]
        public float startAngle = 0f;
        public float minAngle = 0f;
        public float maxAngle = 90f;

        [SerializeField, Tooltip("Si está bloqueado solo vibra un poco al intentar girarlo.")]
        private bool _locked;
        public float lockedFeedbackAngle = 6f;
        public float lockedRattle = 1.2f;
        public UnityEvent onLockedAttempt = new UnityEvent();

        public event Action<float> WhenAngleChanged = delegate { };

        public bool Locked { get => _locked; set => _locked = value; }
        public float Angle { get; private set; }
        public bool IsGrabbed { get; private set; }

        private IGrabbable _grabbable;
        private bool _initialized;
        private Quaternion _baseLocalRotation;
        private Vector3 _prevVector;
        private Quaternion _prevRotation;
        private float _accumulated;
        private float _angleAtGrab;
        private bool _lockedNotified;

        private void Awake() => Init();

        private void Init()
        {
            if (_initialized) return;
            if (target == null) target = transform;
            _baseLocalRotation = target.localRotation * Quaternion.Inverse(Quaternion.AngleAxis(startAngle, localAxis));
            Angle = startAngle;
            _initialized = true;
        }

        public Vector3 WorldAxis => target.TransformDirection(localAxis).normalized;

        public void SetAngle(float angle)
        {
            Init();
            float min = minAngle, max = maxAngle;
            if (_locked)
            {
                min = Mathf.Max(minAngle, _angleAtGrab - lockedRattle);
                max = Mathf.Min(maxAngle, _angleAtGrab + lockedRattle);
            }
            float clamped = Mathf.Clamp(angle, min, max);
            bool changed = !Mathf.Approximately(clamped, Angle);
            Angle = clamped;
            target.localRotation = _baseLocalRotation * Quaternion.AngleAxis(Angle, localAxis);
            if (changed) WhenAngleChanged(Angle);
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
            Pose grab = _grabbable.GrabPoints[0];
            _angleAtGrab = Angle;
            _accumulated = 0f;
            _prevVector = Vector3.ProjectOnPlane(grab.position - target.position, WorldAxis);
            _prevRotation = grab.rotation;
            _lockedNotified = false;
        }

        public void UpdateTransform()
        {
            Pose grab = _grabbable.GrabPoints[0];
            Vector3 axis = WorldAxis;
            float step = 0f;

            // Giro de la mano alrededor del eje
            float orbitStep = 0f;
            Vector3 v = Vector3.ProjectOnPlane(grab.position - target.position, axis);
            float minSqr = mode == RotationMode.OrbitOrTwist ? minOrbitRadius * minOrbitRadius : 1e-6f;
            if (v.sqrMagnitude > minSqr && _prevVector.sqrMagnitude > minSqr)
            {
                orbitStep = Vector3.SignedAngle(_prevVector, v, axis);
            }
            _prevVector = v;

            // Giro de la muñeca alrededor del eje
            float twistStep = 0f;
            Quaternion delta = grab.rotation * Quaternion.Inverse(_prevRotation);
            delta.ToAngleAxis(out float a, out Vector3 deltaAxis);
            if (a > 180f) a -= 360f;
            if (!float.IsNaN(deltaAxis.x) && deltaAxis.sqrMagnitude > 1e-6f)
            {
                twistStep = a * Vector3.Dot(deltaAxis.normalized, axis);
            }
            _prevRotation = grab.rotation;

            switch (mode)
            {
                case RotationMode.Orbit: step = orbitStep; break;
                case RotationMode.Twist: step = twistStep; break;
                default: step = Mathf.Abs(orbitStep) >= Mathf.Abs(twistStep) ? orbitStep : twistStep; break;
            }

            _accumulated += step;

            if (_locked && !_lockedNotified && Mathf.Abs(_accumulated) > lockedFeedbackAngle)
            {
                _lockedNotified = true;
                onLockedAttempt.Invoke();
            }

            SetAngle(_angleAtGrab + _accumulated);
        }

        public void EndTransform()
        {
            IsGrabbed = false;
            if (_locked) SetAngle(_angleAtGrab);
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            Transform t = target != null ? target : transform;
            Vector3 axis = t.TransformDirection(localAxis).normalized;
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(t.position - axis * 0.15f, t.position + axis * 0.15f);
        }
    }
}
