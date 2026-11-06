using Oculus.Interaction;
using UnityEngine;

namespace EscapeRoom
{
    public static class GrabUtil
    {
        /// <summary>Activa/desactiva todos los interactables (grab, hand grab, touch, distance) bajo un objeto.
        /// Desactivarlos obliga a la mano/control a soltar el objeto.</summary>
        public static void SetInteractablesEnabled(GameObject root, bool enabled)
        {
            if (root == null) return;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb is IInteractable) mb.enabled = enabled;
            }
        }
    }
}
