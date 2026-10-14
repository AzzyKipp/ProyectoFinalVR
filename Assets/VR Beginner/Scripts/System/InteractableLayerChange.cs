using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


public class InteractableLayerChange : MonoBehaviour
{
    public XRBaseInteractable TargetInteractable;
    public InteractionLayerMask NewLayerMask;

    public void ChangeLayerDynamic(XRBaseInteractable interactable)
    {
        interactable.interactionLayers = NewLayerMask;
    }

    public void ChangeLayer()
    {
        TargetInteractable.interactionLayers = NewLayerMask;
    }
}
