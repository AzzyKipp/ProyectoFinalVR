using Oculus.Interaction;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Conecta un TeleportInteractable del Interaction SDK de Meta con el efecto del prefab
    /// TeleportationAnchor de VR Beginner: los rayos (TeleporterAnim) aparecen con un fundido
    /// mientras el arco de teleport apunta al punto y se desvanecen al dejar de apuntarlo.
    /// </summary>
    public class TeleportAnchorHighlight : MonoBehaviour
    {
        [Tooltip("TeleportInteractable del punto de teleport (si está vacío se busca en el padre).")]
        [SerializeField, Interface(typeof(IInteractableView))]
        private Object _interactableView;

        [Tooltip("Rayos del efecto (TeleporterBeamHigh / TeleportBeamLow).")]
        public TeleporterAnim[] beams;

        [Tooltip("Objeto opcional que se muestra mientras se apunta al punto.")]
        public GameObject hoverObject;

        private IInteractableView _view;

        public void SetInteractableView(Object view) => _interactableView = view;

        private void Awake()
        {
            _view = _interactableView as IInteractableView;
            if (_view == null && transform.parent != null)
                _view = transform.parent.GetComponentInChildren<IInteractableView>(true);
            if (beams == null || beams.Length == 0) beams = GetComponentsInChildren<TeleporterAnim>(true);
        }

        private void OnEnable()
        {
            if (_view == null) return;
            _view.WhenStateChanged += HandleStateChanged;
            Apply(_view.State);
        }

        private void OnDisable()
        {
            if (_view != null) _view.WhenStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(InteractableStateChangeArgs args) => Apply(args.NewState);

        private void Apply(InteractableState state)
        {
            bool on = state == InteractableState.Hover || state == InteractableState.Select;
            foreach (var beam in beams)
            {
                if (beam == null) continue;
                if (on) beam.StartHighlight();
                else beam.StopHighlight();
            }
            if (hoverObject != null) hoverObject.SetActive(on);
        }
    }
}
