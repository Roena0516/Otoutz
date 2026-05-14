using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class LeverController : MonoBehaviour
{
    [Header("설정")]
    public float sensitivity = 17f;
    public float minPos = -14f;
    public float maxPos = 14f;

    private float currentX = 0f;
    private float prevRaw = 0f;

    public string leverDirection;

    void Update()
    {
        var joystick = Joystick.current;

        if (joystick != null)
        {
            var zAxis = joystick.TryGetChildControl<AxisControl>("z");
            if (zAxis != null)
            {
                float raw = zAxis.ReadValue();

                // 이동 방향 감지
                float delta = raw - prevRaw;
                if (delta < -0.001f)
                    leverDirection = "Left";
                else if (delta > 0.001f)
                    leverDirection = "Right";
                else
                    leverDirection = "Stop";

                prevRaw = raw;

                // -1~1을 minPos~maxPos로 매핑
                float leverNormalized = Mathf.InverseLerp(-0.35f, 0.35f, raw);
                currentX = Mathf.Lerp(minPos, maxPos, leverNormalized);
            }
        }
        else
        {
            // 하드웨어 없을 때 마우스로 대체
            float leverValue = Mouse.current.delta.x.ReadValue() * 0.01f;
            leverValue = Mathf.Clamp(leverValue, -1f, 1f);

            if (leverValue < -0.01f)
                leverDirection = "Left";
            else if (leverValue > 0.01f)
                leverDirection = "Right";
            else
                leverDirection = "Stop";

            currentX = Mathf.Lerp(minPos, maxPos, (leverValue + 1f) / 2f);
        }

        transform.position = new Vector3(currentX, transform.position.y, transform.position.z);
    }
}