using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlanterPreview
{
    [MenuItem("AR Planter/Open Moisture Preview")]
    public static void OpenPreview()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CropBedRoot.prefab");
        if (prefab == null) { Debug.LogError("CropBedRoot prefab is missing."); return; }
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var planter = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        planter.transform.position = Vector3.zero;
        var camera = Camera.main;
        camera.transform.position = new Vector3(0.75f, 0.85f, -1.65f);
        camera.transform.LookAt(new Vector3(0, 0.34f, 0));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.075f, 0.105f, 0.13f);
        camera.nearClipPlane = 0.01f;
        Selection.activeGameObject = planter;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("Underground preview ready. Press Play, choose a plant, then Water selected plant. Normal/Underground changes the view; 1x changes demo speed.");
    }
}
