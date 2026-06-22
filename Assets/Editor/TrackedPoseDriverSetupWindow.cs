using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace VRDemo.EditorTools
{
    /// <summary>
    /// Left/Right Controller 오브젝트에 Tracked Pose Driver (Input System)를 붙이고
    /// Position/Rotation/Tracking State Input에 Action Reference를 자동으로 채워주는 에디터 윈도우.
    /// </summary>
    public class TrackedPoseDriverSetupWindow : EditorWindow
    {
        const string k_DefaultActionAssetName = "XRI Default Input Actions";
        const string k_PositionActionName = "Position";
        const string k_RotationActionName = "Rotation";
        const string k_TrackingStateActionName = "Tracking State";

        InputActionAsset m_ActionAsset;

        GameObject m_LeftController;
        GameObject m_RightController;

        string m_LeftMapName = "XRI Left";
        string m_RightMapName = "XRI Right";

        Vector2 m_Scroll;

        [MenuItem("Tools/VR/Tracked Pose Driver Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<TrackedPoseDriverSetupWindow>();
            window.titleContent = new GUIContent("Pose Driver Setup");
            window.minSize = new Vector2(360f, 320f);
            window.Show();
        }

        void OnEnable()
        {
            if (m_ActionAsset == null)
                m_ActionAsset = FindDefaultActionAsset();

            AutoFindControllers();
        }

        void OnHierarchyChange()
        {
            // 씬에서 오브젝트가 바뀌면 비어있는 슬롯만 다시 채워준다.
            AutoFindControllers();
            Repaint();
        }

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            EditorGUILayout.LabelField("Input Actions", EditorStyles.boldLabel);
            m_ActionAsset = (InputActionAsset)EditorGUILayout.ObjectField(
                "Action Asset", m_ActionAsset, typeof(InputActionAsset), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Controllers", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                m_LeftController = (GameObject)EditorGUILayout.ObjectField(
                    "Left Controller", m_LeftController, typeof(GameObject), true);
                m_LeftMapName = EditorGUILayout.TextField(m_LeftMapName, GUILayout.Width(110f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                m_RightController = (GameObject)EditorGUILayout.ObjectField(
                    "Right Controller", m_RightController, typeof(GameObject), true);
                m_RightMapName = EditorGUILayout.TextField(m_RightMapName, GUILayout.Width(110f));
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Auto Find Controllers"))
                AutoFindControllers(true);

            EditorGUILayout.Space();

            CanSetup(out var reason);
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(reason)))
            {
                if (GUILayout.Button("Add & Assign Tracked Pose Driver", GUILayout.Height(36f)))
                    SetupAll();
            }

            if (!string.IsNullOrEmpty(reason))
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            else
                EditorGUILayout.HelpBox(
                    "각 컨트롤러에 Tracked Pose Driver를 추가(없을 경우)하고 " +
                    "Position/Rotation/Tracking State Input에 해당 맵의 Action Reference를 채웁니다.",
                    MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        bool CanSetup(out string reason)
        {
            if (m_ActionAsset == null)
            {
                reason = "Action Asset이 지정되지 않았습니다.";
                return false;
            }

            if (m_LeftController == null && m_RightController == null)
            {
                reason = "Left/Right Controller가 모두 비어 있습니다.";
                return false;
            }

            reason = null;
            return true;
        }

        void SetupAll()
        {
            int configured = 0;

            if (m_LeftController != null)
                configured += Configure(m_LeftController, m_LeftMapName) ? 1 : 0;

            if (m_RightController != null)
                configured += Configure(m_RightController, m_RightMapName) ? 1 : 0;

            if (configured > 0)
            {
                var scene = GetActiveControllerScene();
                if (scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(scene);

                Debug.Log($"[Pose Driver Setup] {configured}개의 컨트롤러에 Tracked Pose Driver를 설정했습니다.");
            }
        }

        bool Configure(GameObject controller, string mapName)
        {
            var positionRef = FindReference(mapName, k_PositionActionName);
            var rotationRef = FindReference(mapName, k_RotationActionName);
            var trackingStateRef = FindReference(mapName, k_TrackingStateActionName);

            if (positionRef == null || rotationRef == null || trackingStateRef == null)
            {
                Debug.LogError(
                    $"[Pose Driver Setup] '{mapName}' 맵에서 Position/Rotation/Tracking State 액션을 찾지 못했습니다. " +
                    $"(asset: {m_ActionAsset.name})", controller);
                return false;
            }

            var driver = controller.GetComponent<TrackedPoseDriver>();
            if (driver == null)
                driver = Undo.AddComponent<TrackedPoseDriver>(controller);

            Undo.RecordObject(driver, "Assign Tracked Pose Driver Inputs");

            driver.positionInput = new InputActionProperty(positionRef);
            driver.rotationInput = new InputActionProperty(rotationRef);
            driver.trackingStateInput = new InputActionProperty(trackingStateRef);

            EditorUtility.SetDirty(driver);
            return true;
        }

        InputActionReference FindReference(string mapName, string actionName)
        {
            if (m_ActionAsset == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(m_ActionAsset);
            if (string.IsNullOrEmpty(assetPath))
                return null;

            // .inputactions 안에 들어있는 InputActionReference 서브에셋 중에서 맵/액션 이름이 일치하는 것을 찾는다.
            var references = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<InputActionReference>();

            foreach (var reference in references)
            {
                var action = reference.action;
                if (action == null || action.actionMap == null)
                    continue;

                if (action.actionMap.name == mapName && action.name == actionName)
                    return reference;
            }

            return null;
        }

        InputActionAsset FindDefaultActionAsset()
        {
            var guids = AssetDatabase.FindAssets($"t:InputActionAsset {k_DefaultActionAssetName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null && asset.name == k_DefaultActionAssetName)
                    return asset;
            }

            // 이름이 정확히 일치하지 않으면 첫 번째 InputActionAsset이라도 반환.
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        void AutoFindControllers(bool overwrite = false)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            if (overwrite || m_LeftController == null)
                m_LeftController = FindInScene(scene, "Left Controller") ?? m_LeftController;

            if (overwrite || m_RightController == null)
                m_RightController = FindInScene(scene, "Right Controller") ?? m_RightController;
        }

        Scene GetActiveControllerScene()
        {
            if (m_LeftController != null)
                return m_LeftController.scene;
            if (m_RightController != null)
                return m_RightController.scene;
            return SceneManager.GetActiveScene();
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindRecursive(root.transform, name);
                if (found != null)
                    return found.gameObject;
            }

            return null;
        }

        static Transform FindRecursive(Transform current, string name)
        {
            if (current.name == name)
                return current;

            for (int i = 0; i < current.childCount; i++)
            {
                var found = FindRecursive(current.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
