using Oculus.Interaction;
using UnityEngine;

namespace EscapeRoom
{
    /// <summary>
    /// Saca el objeto de su padre la primera vez que se agarra (por ejemplo, el libro dentro del cajón,
    /// para que no se mueva con el cajón al cerrarlo).
    /// </summary>
    public class DetachOnGrab : MonoBehaviour
    {
        public Grabbable grabbable;
        public Transform newParent;
        private bool _done;

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
            if (_done || evt.Type != PointerEventType.Select) return;
            _done = true;
            Transform t = grabbable.Transform != null ? grabbable.Transform : grabbable.transform;
            t.SetParent(newParent, true);
        }
    }
}
