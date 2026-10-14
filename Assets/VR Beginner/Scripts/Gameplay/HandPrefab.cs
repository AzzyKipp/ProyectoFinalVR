using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//Utility script only use as a target of a GetComponent to retrieve the Animator from the hands use as model for the
//controller
public class HandPrefab : MonoBehaviour
{
    public Animator Animator;
    public InputActionProperty grabAction;

    private bool wasGripping = false;

    void Update()
    {
        float grabValue = grabAction.action.ReadValue<float>();
        if(!wasGripping && grabValue > 0)
        {
            Animator.SetTrigger("Selected");
            wasGripping = true;
        }
        else if(wasGripping && grabValue <= 0)
        {
            Animator.SetTrigger("Deselected");
            wasGripping = false;
        }
    }
}
