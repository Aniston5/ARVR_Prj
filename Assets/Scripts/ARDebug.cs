using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARDebug : MonoBehaviour
{
    public ARCameraManager cameraManager;

    private ARSessionState lastState;
    private int frameCount = 0;

    void Start()
    {
        lastState = ARSession.state;
        Debug.Log("AR SESSION START: " + ARSession.state);

        if (cameraManager == null)
            Debug.LogError("CAMERA MANAGER NOT ASSIGNED");
    }

    void Update()
    {
        if (ARSession.state != lastState)
        {
            lastState = ARSession.state;
            Debug.Log("AR SESSION CHANGED: " + lastState);
        }
    }

    void OnEnable()
    {
        if (cameraManager != null)
            cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        if (cameraManager != null)
            cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs args)
    {
        frameCount++;

        if (frameCount % 60 == 0)
        {
            Debug.Log("AR CAMERA FRAMES RECEIVED: " + frameCount);
        }
    }
}