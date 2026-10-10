using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class CropBedPlacement : MonoBehaviour
{
    public GameObject cropBedPrefab;
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    [Tooltip("Design-time planter in MainAR; hidden when AR starts to avoid a duplicate.")]
    public GameObject scenePreview;

    public GameObject PlacedCropBed => placedCropBed;

    private GameObject placedCropBed;

    private static readonly List<ARRaycastHit> hits =
        new List<ARRaycastHit>();

    void Start()
    {
        if (scenePreview != null) scenePreview.SetActive(false);
        if (cropBedPrefab == null || raycastManager == null)
        {
            Debug.LogError("Assign the crop bed prefab and AR raycast manager.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (placedCropBed != null)
            return;

        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (!touch.press.wasPressedThisFrame)
            return;

        Vector2 touchPosition = touch.position.ReadValue();

        if (raycastManager.Raycast(
            touchPosition,
            hits,
            TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            placedCropBed = Instantiate(
                cropBedPrefab,
                hitPose.position,
                hitPose.rotation
            );

            HidePlanes();
        }
    }

    void HidePlanes()
    {
        if (planeManager == null) return;
        foreach (ARPlane plane in planeManager.trackables)
        {
            MeshRenderer renderer =
                plane.GetComponent<MeshRenderer>();

            if (renderer != null)
                renderer.enabled = false;
        }

        // Stop looking for new planes once placement is complete.
        planeManager.enabled = false;
    }
}
