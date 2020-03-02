using UnityEngine;
using UnityEngine.UI;

public class FpsCounter : MonoBehaviour
{
    [SerializeField] private Text _fpsText;
    [SerializeField] private float _hudRefreshRate = 1f;

    private float _timer;

    private void Update()
    {
        if (Time.unscaledTime > _timer)
        {
            int fps = (int)(1f / Time.unscaledDeltaTime);
            _fpsText.text = "FPS: " + fps;
            _timer = Time.unscaledTime + _hudRefreshRate;
        }
    }
}