using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class LeverController : MonoBehaviour
{
    [Header("설정")]
    public float sensitivity = 17f;
    public float minPos = -14f;
    public float maxPos = 14f;
    [Tooltip("마우스 이동량(픽셀) 당 레버 이동 단위")]
    public float mouseSensitivity = 0.05f;

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
            // 하드웨어 없을 때 마우스로 대체: delta(프레임당 이동량)를 위치에 '누적'한다.
            // (절대 위치로 매핑하면 마우스를 멈췄을 때 매 프레임 중앙으로 리셋되는 문제가 생긴다.)
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float deltaX = mouse.delta.x.ReadValue() * mouseSensitivity;

                if (deltaX < -0.001f)
                    leverDirection = "Left";
                else if (deltaX > 0.001f)
                    leverDirection = "Right";
                else
                    leverDirection = "Stop";

                currentX = Mathf.Clamp(currentX + deltaX, minPos, maxPos);
            }
        }

        transform.position = new Vector3(currentX, transform.position.y, transform.position.z);
    }
}