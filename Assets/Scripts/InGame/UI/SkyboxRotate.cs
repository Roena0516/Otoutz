using UnityEngine;

public class SkyboxRotate : MonoBehaviour
{
    void Update()
    {
        float rotation = Time.time * 0.5f;
        RenderSettings.skybox.SetFloat("_Rotation", rotation);
    }
}