using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRDirectInteractor))]
public class HandModelVisibility : MonoBehaviour
{
    [SerializeField] private GameObject handModel;
    
    private XRDirectInteractor interactor;

    private void Awake()
    {
        interactor = GetComponent<XRDirectInteractor>();
    }

    private void OnEnable()
    {
        interactor.selectEntered.AddListener(OnSelectEntered);
        interactor.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        interactor.selectEntered.RemoveListener(OnSelectEntered);
        interactor.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (handModel != null)
            handModel.SetActive(false);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (handModel != null)
            handModel.SetActive(true);
    }
}
