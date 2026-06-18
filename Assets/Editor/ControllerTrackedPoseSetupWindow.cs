using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

public class ControllerTrackedPoseSetupWindow : EditorWindow
{
    const string DefaultInputActionsGuid = "c348712bda248c246b8c49b3db54643f";
    const string Step01ScenePath = "Assets/Scenes/Step_01.unity";

    static readonly string[] PoseActionNames = { "Position", "Rotation", "Tracking State" };

    InputActionAsset m_InputActionAsset;
    string m_ParentName = "Camera Offset";
    string m_LeftControllerName = "Left Controller";
    string m_RightControllerName = "Right Controller";
    string m_LeftActionMapName = "XRI Left";
    string m_RightActionMapName = "XRI Right";
    Vector2 m_ScrollPosition;
    string m_Log = string.Empty;

    [MenuItem("Tools/Controller Tracked Pose Driver Setup")]
    static void OpenWindow()
    {
        var window = GetWindow<ControllerTrackedPoseSetupWindow>("Controller TPD Setup");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    void OnEnable()
    {
        if (m_InputActionAsset == null)
            m_InputActionAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                AssetDatabase.GUIDToAssetPath(DefaultInputActionsGuid));
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Tracked Pose Driver (Input System) 자동 설정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Left / Right Controller GameObject를 생성하고 Tracked Pose Driver를 추가한 뒤, " +
            "Position / Rotation / Tracking State Input에 Action Reference를 연결합니다.",
            MessageType.Info);

        EditorGUILayout.Space();

        m_InputActionAsset = (InputActionAsset)EditorGUILayout.ObjectField(
            "Input Action Asset", m_InputActionAsset, typeof(InputActionAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("생성 위치", EditorStyles.boldLabel);
        m_ParentName = EditorGUILayout.TextField("부모 GameObject 이름", m_ParentName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controllers", EditorStyles.boldLabel);
        m_LeftControllerName = EditorGUILayout.TextField("Left Controller 이름", m_LeftControllerName);
        m_RightControllerName = EditorGUILayout.TextField("Right Controller 이름", m_RightControllerName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Action Map", EditorStyles.boldLabel);
        m_LeftActionMapName = EditorGUILayout.TextField("Left Action Map", m_LeftActionMapName);
        m_RightActionMapName = EditorGUILayout.TextField("Right Action Map", m_RightActionMapName);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(m_InputActionAsset == null))
        {
            if (GUILayout.Button("Controllers 생성 + Tracked Pose Driver 할당", GUILayout.Height(32f)))
                ApplyToScene(SceneManager.GetActiveScene());

            EditorGUILayout.Space(4f);

            if (GUILayout.Button("Step_01 씬에서 실행", GUILayout.Height(24f)))
                ApplyToStep01Scene();
        }

        if (m_InputActionAsset == null)
        {
            EditorGUILayout.HelpBox(
                "Input Action Asset이 필요합니다. 기본값은 XRI Default Input Actions 입니다.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);

        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(string.IsNullOrEmpty(m_Log) ? "아직 실행하지 않았습니다." : m_Log);
        EditorGUILayout.EndScrollView();
    }

    void ApplyToStep01Scene()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(Step01ScenePath))
        {
            m_Log = $"씬을 찾을 수 없습니다: {Step01ScenePath}";
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(Step01ScenePath, OpenSceneMode.Single);
        ApplyToScene(scene);
    }

    void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            m_Log = "유효한 씬이 열려 있지 않습니다.";
            return;
        }

        if (m_InputActionAsset == null)
        {
            m_Log = "Input Action Asset이 지정되지 않았습니다.";
            return;
        }

        var actionReferences = BuildActionReferenceLookup(m_InputActionAsset);
        var log = new StringBuilder();
        log.AppendLine($"씬: {scene.name}");
        log.AppendLine($"Input Action Asset: {m_InputActionAsset.name}");
        log.AppendLine();

        var parent = FindGameObjectInScene(scene, m_ParentName);
        if (parent == null)
        {
            m_Log = log + $"[실패] 부모 GameObject '{m_ParentName}'를 찾을 수 없습니다.";
            Repaint();
            return;
        }

        log.AppendLine($"부모: {GetTransformPath(parent.transform)}");
        log.AppendLine();

        var changed = false;

        changed |= EnsureController(scene, parent.transform, m_LeftControllerName, log);
        changed |= EnsureController(scene, parent.transform, m_RightControllerName, log);

        log.AppendLine();

        changed |= SetupController(scene, m_LeftControllerName, m_LeftActionMapName, actionReferences, log);
        changed |= SetupController(scene, m_RightControllerName, m_RightActionMapName, actionReferences, log);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            log.AppendLine("완료: 변경 사항이 씬에 저장되었습니다. (Ctrl+S로 저장하세요)");
        }
        else
        {
            log.AppendLine("변경 사항 없음.");
        }

        m_Log = log.ToString();
        Repaint();
    }

    static bool EnsureController(Scene scene, Transform parent, string controllerName, StringBuilder log)
    {
        var controller = FindGameObjectInScene(scene, controllerName);
        if (controller != null)
        {
            log.AppendLine($"[확인] '{controllerName}' 이미 존재 ({GetTransformPath(controller.transform)})");
            return false;
        }

        controller = new GameObject(controllerName);
        Undo.RegisterCreatedObjectUndo(controller, "Create Controller");
        controller.transform.SetParent(parent, false);
        controller.transform.localPosition = Vector3.zero;
        controller.transform.localRotation = Quaternion.identity;
        controller.transform.localScale = Vector3.one;

        log.AppendLine($"[생성] '{controllerName}' GameObject 생성 ({GetTransformPath(controller.transform)})");
        return true;
    }

    static string GetTransformPath(Transform transform)
    {
        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    static bool SetupController(
        Scene scene,
        string controllerName,
        string actionMapName,
        Dictionary<(string mapName, string actionName), InputActionReference> actionReferences,
        StringBuilder log)
    {
        var controller = FindGameObjectInScene(scene, controllerName);
        if (controller == null)
        {
            log.AppendLine($"[실패] '{controllerName}' GameObject를 찾을 수 없습니다.");
            return false;
        }

        var driver = controller.GetComponent<TrackedPoseDriver>();
        if (driver == null)
        {
            driver = Undo.AddComponent<TrackedPoseDriver>(controller);
            log.AppendLine($"[추가] '{controllerName}'에 Tracked Pose Driver 컴포넌트 추가");
        }
        else
        {
            log.AppendLine($"[갱신] '{controllerName}'의 Tracked Pose Driver 설정 업데이트");
        }

        Undo.RecordObject(driver, "Setup Controller Tracked Pose Driver");

        var updatedAny = false;
        updatedAny |= AssignActionReference(actionReferences, actionMapName, "Position", reference =>
        {
            driver.positionInput = new InputActionProperty(reference);
        });
        updatedAny |= AssignActionReference(actionReferences, actionMapName, "Rotation", reference =>
        {
            driver.rotationInput = new InputActionProperty(reference);
        });
        updatedAny |= AssignActionReference(actionReferences, actionMapName, "Tracking State", reference =>
        {
            driver.trackingStateInput = new InputActionProperty(reference);
        });

        driver.ignoreTrackingState = false;

        if (updatedAny)
            EditorUtility.SetDirty(driver);

        foreach (var actionName in PoseActionNames)
        {
            if (TryGetActionReference(actionReferences, actionMapName, actionName, out _))
                log.AppendLine($"  - {actionMapName}/{actionName} 연결됨");
            else
                log.AppendLine($"  - [실패] {actionMapName}/{actionName} Action Reference 없음");
        }

        log.AppendLine();
        return updatedAny;
    }

    static bool AssignActionReference(
        Dictionary<(string mapName, string actionName), InputActionReference> actionReferences,
        string actionMapName,
        string actionName,
        System.Action<InputActionReference> assign)
    {
        if (!TryGetActionReference(actionReferences, actionMapName, actionName, out var reference))
            return false;

        assign(reference);
        return true;
    }

    static bool TryGetActionReference(
        Dictionary<(string mapName, string actionName), InputActionReference> actionReferences,
        string actionMapName,
        string actionName,
        out InputActionReference reference)
    {
        return actionReferences.TryGetValue((actionMapName, actionName), out reference);
    }

    static Dictionary<(string mapName, string actionName), InputActionReference> BuildActionReferenceLookup(
        InputActionAsset asset)
    {
        var lookup = new Dictionary<(string mapName, string actionName), InputActionReference>();
        var assetPath = AssetDatabase.GetAssetPath(asset);

        foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (subAsset is not InputActionReference actionReference || actionReference.action == null)
                continue;

            var key = (actionReference.action.actionMap.name, actionReference.action.name);
            lookup[key] = actionReference;
        }

        return lookup;
    }

    static GameObject FindGameObjectInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindInHierarchy(root.transform, objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    static GameObject FindInHierarchy(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root.gameObject;

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindInHierarchy(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
