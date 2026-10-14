using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


/// <summary>
/// Since we can't make an Interactable non interactive, we use that to remove/add the Hands layer to the mask of the
/// Interactable, making it in effect non interactable with the direct Controllers.
/// </summary>
public class DisableInteractable : MonoBehaviour
{
    public void DisableInteraction(XRBaseInteractable interactable)
    {
        interactable.interactionLayers &= ~(1<<LayerMask.NameToLayer("Hands"));
    }

    public void EnableInteraction(XRBaseInteractable interactable)
    {
        interactable.interactionLayers |= (1<<LayerMask.NameToLayer("Hands"));
    }
}
