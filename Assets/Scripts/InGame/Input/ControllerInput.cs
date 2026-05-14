using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ControllerInput : MonoBehaviour
{
    void Update()
    {
        var joystick = Joystick.current;
        if (joystick == null) return;

        var zAxis = joystick.TryGetChildControl<AxisControl>("z");
        if (zAxis != null)
        {
            float lever = zAxis.ReadValue(); // -1 ~ 1
            Debug.Log("레버: " + lever);
        }
    }
}