using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Subclass of the classic Socket Interactor from the Interaction toolkit that will only accept object with the right
/// SocketTarget 
/// </summary>
public class XRExclusiveSocketInteractor : XRSocketInteractor
{
    public string AcceptedType;

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        var baseInteractable = interactable as XRBaseInteractable;
        SocketTarget socketTarget = baseInteractable.GetComponent<SocketTarget>();

        if (socketTarget == null)
            return false;

        return base.CanSelect(interactable) && (socketTarget.SocketType == AcceptedType);
    }

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return CanSelect(interactable as IXRSelectInteractable);
    }
}
