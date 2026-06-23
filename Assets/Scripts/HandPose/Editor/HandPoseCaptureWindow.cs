using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

public class HandPoseCaptureWindow : EditorWindow
{
    const string HandPracScenePath = "Assets/Scenes/Hand_Prac.unity";
    const string CapturerObjectName = "Hand Pose Capturer";

    [SerializeField]
    string m_NextPoseName = "HandOpen";

    [SerializeField]
    Handedness m_CaptureHandedness = Handedness.Left;

    Vector2 m_PoseListScrollPosition;
    Vector2 m_BoneListScrollPosition;
    bool m_IsListening;
    bool m_ShowBoneDetails;
    bool m_PreviewInFrontOfCamera = true;

    HandPoseData m_AnimationStartPose;
    HandPoseData m_AnimationEndPose;
    HandPoseAnimationRig m_AnimationRig = HandPoseAnimationRig.MetaLeftHand;
    string m_TransitionClipName = "HandOpen_to_HandHalfGrip";
    string m_AnimationOutputFolder = HandPoseAnimationGenerator.DefaultOutputFolder;
    bool m_AssignToHandController = true;

    readonly List<HandPoseData> m_PoseAssets = new();
    int m_SelectedPoseIndex = -1;

    [MenuItem("Tools/Hand Pose Capture")]
    static void OpenWindow()
    {
        var window = GetWindow<HandPoseCaptureWindow>("Hand Pose Capture");
        window.minSize = new Vector2(480f, 720f);
        window.Show();
    }

    void OnEnable()
    {
        HandPoseCaptureSession.PoseCaptured += OnPoseCaptured;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        m_IsListening = HandPoseCaptureSession.IsListening;
        // 도메인 리로드(Play 시작)로 Session 값이 초기화될 수 있으므로,
        // 창에 저장된 손 선택을 다시 Session에 적용합니다.
        HandPoseCaptureSession.CaptureHandedness = m_CaptureHandedness;
        m_AnimationRig = HandPoseRigUtility.GetDefaultMetaRig(m_CaptureHandedness);
        ResetAnimationPosesForHand(m_CaptureHandedness);
        RefreshPoseAssets();
        TryAssignDefaultAnimationPoses();
    }

    void OnDisable()
    {
        HandPoseCaptureSession.PoseCaptured -= OnPoseCaptured;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        HandPoseCaptureSession.IsListening = false;
        HandPoseEditorPreview.ClearPreview();
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            m_IsListening = false;
            HandPoseCaptureSession.IsListening = false;
            HandPoseEditorPreview.ClearPreview();
        }

        RefreshPoseAssets();
        Repaint();
    }

    HandPoseData SelectedPose =>
        m_SelectedPoseIndex >= 0 && m_SelectedPoseIndex < m_PoseAssets.Count
            ? m_PoseAssets[m_SelectedPoseIndex]
            : null;

    void OnGUI()
    {
        EditorGUILayout.LabelField("Hand_Prac — XR Hand 포즈 캡처", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Play Mode에서 선택한 손 포즈를 그립 버튼으로 캡처하면 ScriptableObject로 저장됩니다.\n" +
            $"저장 위치: {HandPoseCaptureSession.DefaultPoseFolder}\n" +
            "캡처 시 손목(Wrist)을 기준으로 (0,0,0) 좌표계에 정규화되어 저장됩니다.",
            MessageType.Info);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        m_CaptureHandedness = (Handedness)EditorGUILayout.EnumPopup("캡처할 손", m_CaptureHandedness);
        if (EditorGUI.EndChangeCheck())
        {
            HandPoseCaptureSession.CaptureHandedness = m_CaptureHandedness;
            m_AnimationRig = HandPoseRigUtility.GetDefaultMetaRig(m_CaptureHandedness);
            ResetAnimationPosesForHand(m_CaptureHandedness);
        }

        m_NextPoseName = EditorGUILayout.TextField("다음 포즈 이름", m_NextPoseName);
        HandPoseCaptureSession.NextPoseName = m_NextPoseName;

        DrawSceneSetupSection();
        DrawCaptureSection();
        DrawSavedPosesSection();
        DrawAnimationSection();
    }

    void DrawSceneSetupSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("씬 설정", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Hand_Prac 씬 열기"))
                    OpenHandPracScene();
            }

            if (GUILayout.Button(Application.isPlaying ? "캡처러 추가 (런타임)" : "캡처러 추가"))
                EnsureCapturerInActiveScene();
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play Mode에서 추가한 캡처러는 종료 시 사라집니다. 영구 사용하려면 Edit Mode에서 '캡처러 추가' 후 씬을 저장하세요.",
                MessageType.None);
        }
    }

    void DrawCaptureSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("캡처 (Play Mode)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUI.BeginChangeCheck();
            m_IsListening = EditorGUILayout.Toggle("그립 버튼으로 캡처 대기", m_IsListening);
            if (EditorGUI.EndChangeCheck())
                HandPoseCaptureSession.IsListening = m_IsListening;

            if (GUILayout.Button("지금 포즈 캡처 (그립 없이)", GUILayout.Height(28f)))
                CaptureFromEditorButton();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                $"Play Mode에서 Hand Visualizer가 {m_CaptureHandedness} 손을 추적할 때 캡처할 수 있습니다.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(HandPoseCaptureSession.StatusMessage, MessageType.None);

            if (!HandPoseCaptureSession.IsListening)
            {
                EditorGUILayout.HelpBox(
                    "스켈레톤(관절 디버그)은 '그립 버튼으로 캡처 대기'를 켜야 표시됩니다.\n" +
                    "항상 보이게 하려면 Hand Visualizer에서 Debug Draw Joints를 켜고,\n" +
                    "HandPoseCaptureVisibilitySupport의 Only Show Skeleton While Capture Listening을 끄세요.",
                    MessageType.Info);
            }

            if (!FindCapturerInActiveScene())
            {
                EditorGUILayout.HelpBox(
                    "씬에 HandPoseCaptureController가 없습니다. '캡처러 추가'를 눌러주세요.",
                    MessageType.Warning);
            }
        }
    }

    void DrawSavedPosesSection()
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("저장된 포즈", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("새로고침", GUILayout.Width(72f)))
                RefreshPoseAssets();
            if (GUILayout.Button("레거시 변환", GUILayout.Width(88f)))
                MigrateAllLegacyPoses();
        }

        if (m_PoseAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("아직 저장된 포즈가 없습니다.", MessageType.Info);
            return;
        }

        m_PoseListScrollPosition = EditorGUILayout.BeginScrollView(
            m_PoseListScrollPosition,
            GUILayout.MinHeight(120f),
            GUILayout.MaxHeight(180f));

        for (var i = 0; i < m_PoseAssets.Count; i++)
        {
            var pose = m_PoseAssets[i];
            if (pose == null)
                continue;

            var isSelected = i == m_SelectedPoseIndex;
            var style = isSelected ? EditorStyles.helpBox : EditorStyles.inspectorDefaultMargins;

            EditorGUILayout.BeginVertical(style);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(isSelected, $"{pose.PoseName} ({pose.Handedness})", EditorStyles.radioButton))
                {
                    if (!isSelected)
                    {
                        m_SelectedPoseIndex = i;
                        GUI.FocusControl(null);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField($"{pose.Bones.Count} bones", GUILayout.Width(64f));
            }

            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(pose), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        DrawSelectedPosePreview();
    }

    void DrawAnimationSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("애니메이션 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Meta Left/Right: L_* 또는 R_* 본 경로 (MetaHand_L / MetaHand_R).\n" +
            "Oculus Left/Right: b_l_* 또는 b_r_* 본 경로 (OculusHand_L / OculusHand_R).",
            MessageType.Info);

        m_AnimationStartPose = (HandPoseData)EditorGUILayout.ObjectField(
            "시작 포즈 (Open)",
            m_AnimationStartPose,
            typeof(HandPoseData),
            false);

        m_AnimationEndPose = (HandPoseData)EditorGUILayout.ObjectField(
            "끝 포즈 (Open/Grip/HalfGrip)",
            m_AnimationEndPose,
            typeof(HandPoseData),
            false);

        m_AnimationRig = (HandPoseAnimationRig)EditorGUILayout.EnumPopup("대상 리그", m_AnimationRig);
        m_TransitionClipName = EditorGUILayout.TextField("전환 클립 이름", m_TransitionClipName);
        m_AnimationOutputFolder = EditorGUILayout.TextField("저장 폴더", m_AnimationOutputFolder);
        m_AssignToHandController = EditorGUILayout.Toggle(
            "손 Animator Controller에 연결",
            m_AssignToHandController);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open / Grip 클립 생성", GUILayout.Height(28f)))
                GenerateOpenGripClips();

            if (GUILayout.Button("전환 클립 생성", GUILayout.Height(28f)))
                GenerateTransitionClip();
        }

        if (m_AnimationStartPose == null || m_AnimationEndPose == null)
        {
            EditorGUILayout.HelpBox("시작·끝 포즈를 지정하세요. 목록에서 선택한 포즈를 쓰려면 아래 버튼을 사용할 수 있습니다.", MessageType.None);

            using (new EditorGUI.DisabledScope(SelectedPose == null))
            {
                if (GUILayout.Button("선택 포즈 → 시작 포즈로"))
                    m_AnimationStartPose = SelectedPose;

                if (GUILayout.Button("선택 포즈 → 끝 포즈로"))
                    m_AnimationEndPose = SelectedPose;
            }
        }
    }

    void ResetAnimationPosesForHand(Handedness handedness)
    {
        if (m_AnimationStartPose != null && m_AnimationStartPose.Handedness != handedness)
            m_AnimationStartPose = null;
        if (m_AnimationEndPose != null && m_AnimationEndPose.Handedness != handedness)
            m_AnimationEndPose = null;

        TryAssignDefaultAnimationPoses(handedness);
    }

    void TryAssignDefaultAnimationPoses()
    {
        TryAssignDefaultAnimationPoses(m_CaptureHandedness);
    }

    void TryAssignDefaultAnimationPoses(Handedness handedness)
    {
        if (m_AnimationStartPose == null)
        {
            m_AnimationStartPose = handedness == Handedness.Right
                ? FindPoseForHand(handedness, "HandOpen_Right", "HandOpen")
                : FindPoseForHand(handedness, "HandOpen", "HandOpen_Left", "HandOpen_Left0");
        }

        if (m_AnimationEndPose == null)
        {
            m_AnimationEndPose = handedness == Handedness.Right
                ? FindPoseForHand(handedness, "HandGrip_Right", "HandHalfGrip_Right", "HandGrip", "HandHalfGrip")
                : FindPoseForHand(handedness, "HandHalfGrip", "HandHalfGrip_Left", "HandGrip_Left", "HandGrip", "HandGripRe");
        }
    }

    HandPoseData FindPoseForHand(Handedness handedness, params string[] preferredNames)
    {
        foreach (var preferredName in preferredNames)
        {
            foreach (var pose in m_PoseAssets)
            {
                if (pose != null && pose.PoseName == preferredName && pose.Handedness == handedness)
                    return pose;
            }
        }

        foreach (var preferredName in preferredNames)
        {
            foreach (var pose in m_PoseAssets)
            {
                if (pose == null || pose.Handedness != handedness)
                    continue;

                if (pose.PoseName != null &&
                    pose.PoseName.IndexOf(preferredName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return pose;
            }
        }

        return null;
    }

    HandPoseData FindPoseByName(string poseName)
    {
        foreach (var pose in m_PoseAssets)
        {
            if (pose != null && pose.PoseName == poseName)
                return pose;
        }

        return null;
    }

    bool ValidateAnimationPoses(out string error)
    {
        if (m_AnimationStartPose == null || m_AnimationEndPose == null)
        {
            error = "시작 포즈와 끝 포즈를 모두 지정하세요.";
            return false;
        }

        if (!m_AnimationStartPose.IsWristAtOrigin())
        {
            error = $"시작 포즈 '{m_AnimationStartPose.PoseName}'가 손목 기준 좌표계가 아닙니다. 먼저 변환하세요.";
            return false;
        }

        if (!m_AnimationEndPose.IsWristAtOrigin())
        {
            error = $"끝 포즈 '{m_AnimationEndPose.PoseName}'가 손목 기준 좌표계가 아닙니다. 먼저 변환하세요.";
            return false;
        }

        if (m_AnimationStartPose.Handedness != m_AnimationEndPose.Handedness)
        {
            error = "시작 포즈와 끝 포즈의 손(Left/Right)이 다릅니다.";
            return false;
        }

        error = null;
        return true;
    }

    void GenerateOpenGripClips()
    {
        if (!ValidateAnimationPoses(out var error))
        {
            EditorUtility.DisplayDialog("애니메이션 생성", error, "OK");
            return;
        }

        var openClipName = HandPoseAnimationGenerator.InferClipNameFromPose(m_AnimationStartPose);
        var gripClipName = HandPoseAnimationGenerator.InferClipNameFromPose(m_AnimationEndPose);

        var (openPath, gripPath) = HandPoseAnimationGenerator.GenerateOpenGripClips(
            m_AnimationStartPose,
            m_AnimationEndPose,
            m_AnimationRig,
            m_AnimationOutputFolder,
            openClipName,
            gripClipName);

        var message = $"생성됨:\n{openPath}\n{gripPath}";

        if (m_AssignToHandController)
        {
            if (HandPoseAnimationGenerator.TryAssignToHandBlendTree(m_AnimationRig, openPath, gripPath))
                message += $"\n\n{HandPoseAnimationGenerator.GetControllerPath(m_AnimationRig)} 블렌드 트리에 연결했습니다.";
            else
                message += "\n\n컨트롤러 블렌드 트리 연결에 실패했습니다.";

            if (HandPoseRigUtility.IsOculusRig(m_AnimationRig) == false &&
                HandPoseAnimationGenerator.TryAssignControllerToSceneHand(m_AnimationRig))
            {
                var handLabel = HandPoseRigUtility.IsRightRig(m_AnimationRig) ? "RightHand" : "LeftHand";
                message += $"\n씬 {handLabel} Animator에 {System.IO.Path.GetFileNameWithoutExtension(HandPoseAnimationGenerator.GetControllerPath(m_AnimationRig))}을 할당했습니다.";
            }
            else if (!HandPoseRigUtility.IsOculusRig(m_AnimationRig))
            {
                var handLabel = HandPoseRigUtility.IsRightRig(m_AnimationRig) ? "RightHand" : "LeftHand";
                message += $"\n씬에서 {handLabel} Animator를 찾지 못했습니다.";
            }
        }

        EditorUtility.DisplayDialog("애니메이션 생성", message, "OK");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AnimationClip>(gripPath));
    }

    void GenerateTransitionClip()
    {
        if (!ValidateAnimationPoses(out var error))
        {
            EditorUtility.DisplayDialog("애니메이션 생성", error, "OK");
            return;
        }

        var clipName = string.IsNullOrWhiteSpace(m_TransitionClipName)
            ? $"{m_AnimationStartPose.PoseName}_to_{m_AnimationEndPose.PoseName}"
            : m_TransitionClipName;

        var path = HandPoseAnimationGenerator.GenerateTransitionClip(
            m_AnimationStartPose,
            m_AnimationEndPose,
            clipName,
            m_AnimationRig,
            m_AnimationOutputFolder);

        EditorUtility.DisplayDialog("애니메이션 생성", $"생성됨:\n{path}", "OK");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<AnimationClip>(path));
    }

    void DrawSelectedPosePreview()
    {
        var pose = SelectedPose;
        if (pose == null)
        {
            EditorGUILayout.HelpBox("목록에서 포즈를 선택하면 미리보기할 수 있습니다.", MessageType.None);
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("선택된 포즈", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("이름", pose.PoseName);
        EditorGUILayout.LabelField("손", pose.Handedness.ToString());
        EditorGUILayout.LabelField("캡처 시각", string.IsNullOrEmpty(pose.CapturedAtUtc) ? "-" : pose.CapturedAtUtc);
        EditorGUILayout.LabelField("본 개수", pose.Bones.Count.ToString());
        EditorGUILayout.LabelField(
            "좌표계",
            pose.IsWristAtOrigin() ? "손목 기준 (0,0,0)" : "레거시 (변환 필요)");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("씬에 적용", GUILayout.Height(26f)))
            {
                if (HandPoseEditorPreview.TryApplyPreview(pose, out var message, m_PreviewInFrontOfCamera))
                    ShowNotification(new GUIContent(message));
                else
                    EditorUtility.DisplayDialog("포즈 미리보기", message, "OK");
            }

            if (GUILayout.Button("미리보기 해제", GUILayout.Height(26f)))
            {
                HandPoseEditorPreview.ClearPreview();
                Repaint();
            }

            if (GUILayout.Button("에셋 선택", GUILayout.Height(26f)))
            {
                Selection.activeObject = pose;
                EditorGUIUtility.PingObject(pose);
            }
        }

        m_PreviewInFrontOfCamera = EditorGUILayout.Toggle(
            new GUIContent(
                "Game View용 — Main Camera 앞에 배치",
                "LeftHand 루트만 (0,0,0)으로 옮겨도 L_Wrist 본이 예전 트래킹 좌표에 남아 Game View에서 안 보일 수 있습니다. 이 옵션을 켜면 카메라 앞에 포즈를 적용합니다."),
            m_PreviewInFrontOfCamera);

        if (!pose.IsWristAtOrigin())
        {
            EditorGUILayout.HelpBox("레거시 좌표계입니다. 아래 버튼으로 손목 (0,0,0) 기준으로 변환할 수 있습니다.", MessageType.Warning);
            if (GUILayout.Button("이 포즈를 손목 기준으로 변환", GUILayout.Height(24f)))
                MigratePoseAsset(pose);
        }

        if (!string.IsNullOrEmpty(HandPoseEditorPreview.StatusMessage))
            EditorGUILayout.HelpBox(HandPoseEditorPreview.StatusMessage, MessageType.None);

        m_ShowBoneDetails = EditorGUILayout.Foldout(m_ShowBoneDetails, "본 목록");
        if (m_ShowBoneDetails)
        {
            m_BoneListScrollPosition = EditorGUILayout.BeginScrollView(
                m_BoneListScrollPosition,
                GUILayout.MaxHeight(140f));

            foreach (var bone in pose.Bones)
            {
                EditorGUILayout.LabelField(
                    $"{bone.boneName}  pos={bone.localPosition:F3}  rot={bone.localRotation.eulerAngles:F0}",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    void OnPoseCaptured(HandPoseSnapshot snapshot)
    {
        SavePoseAsset(snapshot);
        RefreshPoseAssets();

        var savedName = snapshot.PoseName;
        m_SelectedPoseIndex = m_PoseAssets.FindIndex(p => p != null && p.PoseName == savedName);
        Repaint();
    }

    void CaptureFromEditorButton()
    {
        var capturer = FindCapturerInActiveScene();
        if (capturer == null)
        {
            HandPoseCaptureSession.SetStatus("HandPoseCaptureController가 씬에 없습니다.");
            return;
        }

        capturer.CaptureNow();
        Repaint();
    }

    static void SavePoseAsset(HandPoseSnapshot snapshot)
    {
        EnsurePoseFolderExists();

        var safeName = SanitizeAssetName(snapshot.PoseName);
        var assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{HandPoseCaptureSession.DefaultPoseFolder}/{safeName}.asset");

        var asset = CreateInstance<HandPoseData>();
        asset.SetPose(snapshot.PoseName, snapshot.Handedness, snapshot.Bones);

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        HandPoseCaptureSession.SetStatus($"저장됨: {assetPath}");
    }

    static void EnsurePoseFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Poses"))
            AssetDatabase.CreateFolder("Assets", "Poses");
    }

    static string SanitizeAssetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "HandPose";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
            builder.Append(System.Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);

        return builder.Length == 0 ? "HandPose" : builder.ToString();
    }

    void RefreshPoseAssets()
    {
        EnsurePoseFolderExists();

        m_PoseAssets.Clear();
        var guids = AssetDatabase.FindAssets("t:HandPoseData", new[] { HandPoseCaptureSession.DefaultPoseFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var pose = AssetDatabase.LoadAssetAtPath<HandPoseData>(path);
            if (pose != null)
                m_PoseAssets.Add(pose);
        }

        m_PoseAssets.Sort((a, b) => string.CompareOrdinal(a.PoseName, b.PoseName));

        if (m_SelectedPoseIndex >= m_PoseAssets.Count)
            m_SelectedPoseIndex = m_PoseAssets.Count - 1;

        TryAssignDefaultAnimationPoses();
        Repaint();
    }

    static int MigrateAllLegacyPoses()
    {
        EnsurePoseFolderExists();

        var migrated = 0;
        var guids = AssetDatabase.FindAssets("t:HandPoseData", new[] { HandPoseCaptureSession.DefaultPoseFolder });
        foreach (var guid in guids)
        {
            var pose = AssetDatabase.LoadAssetAtPath<HandPoseData>(AssetDatabase.GUIDToAssetPath(guid));
            if (MigratePoseAsset(pose))
                migrated++;
        }

        if (migrated > 0)
        {
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Hand Pose Capture", $"{migrated}개 포즈를 손목 기준 좌표계로 변환했습니다.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Hand Pose Capture", "변환할 레거시 포즈가 없습니다.", "OK");
        }

        return migrated;
    }

    public static bool MigratePoseAsset(HandPoseData pose)
    {
        if (pose == null || !pose.NeedsWristOriginMigration())
            return false;

        if (!pose.TryMigrateLegacyToWristOrigin())
            return false;

        EditorUtility.SetDirty(pose);
        return true;
    }

    static void OpenHandPracScene()
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(HandPracScenePath))
        {
            EditorUtility.DisplayDialog("Hand Pose Capture", $"씬을 찾을 수 없습니다:\n{HandPracScenePath}", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(HandPracScenePath, OpenSceneMode.Single);
    }

    static void EnsureCapturerInActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hand Pose Capture", "유효한 씬이 열려 있지 않습니다.", "OK");
            return;
        }

        if (FindCapturerInActiveScene() != null)
        {
            if (!Application.isPlaying)
                EditorUtility.DisplayDialog("Hand Pose Capture", "이미 HandPoseCaptureController가 있습니다.", "OK");
            else
                HandPoseCaptureSession.SetStatus("이미 HandPoseCaptureController가 있습니다.");

            return;
        }

        var capturerObject = new GameObject(CapturerObjectName);
        capturerObject.AddComponent<HandPoseCaptureController>();

        if (Application.isPlaying)
        {
            Selection.activeGameObject = capturerObject;
            HandPoseCaptureSession.SetStatus(
                $"'{CapturerObjectName}'를 Play Mode에 추가했습니다. (Play 종료 시 사라짐)");
            return;
        }

        Undo.RegisterCreatedObjectUndo(capturerObject, "Add Hand Pose Capturer");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = capturerObject;

        EditorUtility.DisplayDialog(
            "Hand Pose Capture",
            $"'{CapturerObjectName}' 오브젝트를 추가했습니다.\n씬을 저장(Ctrl+S)한 뒤 Play Mode에서 사용하세요.",
            "OK");
    }

    static HandPoseCaptureController FindCapturerInActiveScene()
    {
        var capturers = Object.FindObjectsByType<HandPoseCaptureController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        return capturers.Length > 0 ? capturers[0] : null;
    }
}
