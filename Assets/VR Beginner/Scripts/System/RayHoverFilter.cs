using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRInteractorLineVisual))]
public class RayHoverFilter : MonoBehaviour, IXRHoverFilter
{
    public bool canProcess => isActiveAndEnabled;

    private XRInteractorLineVisual lineVisual;

    private void Start()
    {
        lineVisual = GetComponent<XRInteractorLineVisual>();
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return lineVisual != null && lineVisual.enabled;
    }
}