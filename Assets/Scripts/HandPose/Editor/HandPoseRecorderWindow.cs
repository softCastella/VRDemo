using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Hands;

// 핸드트래킹으로 잡은 현재 손모양을 한 번에 저장하는 에디터 툴.
//  - Record: HandPoseData 에셋으로 저장(재적용/리타게팅용)
//  - Export: 1프레임 AnimationClip(.anim)으로 저장(Oculus Grip_L 과 동일한 방식, LeftHand 본 기준)
// 핸드트래킹은 Play 모드에서만 동작하므로, Play 중 손이 추적되는 상태에서 사용한다.
public class HandPoseRecorderWindow : EditorWindow
{
    [SerializeField] Handedness m_Handedness = Handedness.Left;
    [SerializeField] bool m_AutoFind = true;
    [SerializeField] XRHandSkeletonDriver m_Driver;

    [SerializeField] string m_SaveFolder = "Assets/HandPoses";
    [SerializeField] string m_PoseName = "HandPose";
    [SerializeField] bool m_RecordRootPose;

    [SerializeField] string m_ClipFolder = "Assets/HandPoses/Clips";
    [SerializeField] string m_ClipName = "HandPose_Grip";
    [SerializeField] HandPoseAnimationRig m_ExportRig = HandPoseAnimationRig.MetaLeftHand;

    [SerializeField] string m_PrefabFolder = "Assets/HandPoses/Prefabs";
    [SerializeField] string m_PrefabName = "HandPose_Snapshot";
    [SerializeField] bool m_SavePrefabOnAttach = true;
    [SerializeField] GameObject m_AttachTarget; // Attach 대상 부모 오브젝트

    // 지연 캡처: 버튼을 누른 뒤 손을 타깃에 잡고 유지하면, 카운트다운 종료 시점의 손 포즈로 캡처한다.
    // (헤드셋 사용 시 마우스로 버튼을 누르는 순간 손이 타깃에서 빠져버리는 문제를 방지)
    [SerializeField] float m_CaptureDelay = 3f;
    bool m_CapturePending;
    double m_CaptureFireTime;

    [SerializeField] HandPoseData m_SourceData; // 에셋 -> 클립 변환용

    const string k_GhostShaderPath = "Assets/Shaders/GhostHand.shader";
    const string k_GhostMaterialPath = "Assets/Materials/GhostHand.mat";
    const string k_GhostShaderName = "Custom/GhostHand";

    static Material s_GhostMaterial;

    [MenuItem("Tools/Hand Pose Recorder")]
    static void Open() => GetWindow<HandPoseRecorderWindow>("Hand Pose Recorder");

    void OnEnable() => EditorApplication.update += OnEditorUpdate;
    void OnDisable() => EditorApplication.update -= OnEditorUpdate;

    void OnEditorUpdate()
    {
        // Play 모드를 벗어나면 대기 중인 지연 캡처는 취소
        if (!Application.isPlaying)
        {
            if (m_CapturePending)
            {
                m_CapturePending = false;
                Repaint();
            }
            return;
        }

        // 지연 캡처 카운트다운 처리
        if (m_CapturePending && EditorApplication.timeSinceStartup >= m_CaptureFireTime)
        {
            m_CapturePending = false;

            var driver = ResolveDriver();
            if (driver == null)
                Debug.LogWarning("[HandPoseRecorder] 지연 캡처 실패: XRHandSkeletonDriver를 찾지 못했습니다. 손이 추적되고 있는지 확인하세요.");
            else if (m_AttachTarget == null)
                Debug.LogWarning("[HandPoseRecorder] 지연 캡처 실패: Attach Target이 비어 있습니다.");
            else
                AttachPosedHand(driver, m_AttachTarget);
        }

        Repaint();
    }

    XRHandSkeletonDriver ResolveDriver()
    {
        if (!m_AutoFind)
            return m_Driver;

        var drivers = FindObjectsByType<XRHandSkeletonDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var d in drivers)
        {
            var evts = d.handTrackingEvents;
            if (evts != null && evts.handedness == m_Handedness)
                return d;
        }

        return null;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Hand Pose Recorder", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        m_Handedness = (Handedness)EditorGUILayout.EnumPopup("Handedness", m_Handedness);
        if (EditorGUI.EndChangeCheck())
            m_ExportRig = HandPoseRigUtility.GetDefaultMetaRig(m_Handedness);
        m_AutoFind = EditorGUILayout.Toggle("Auto Find Driver", m_AutoFind);
        using (new EditorGUI.DisabledScope(m_AutoFind))
            m_Driver = (XRHandSkeletonDriver)EditorGUILayout.ObjectField("Skeleton Driver", m_Driver, typeof(XRHandSkeletonDriver), true);

        XRHandSkeletonDriver driver = Application.isPlaying ? ResolveDriver() : null;

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("핸드트래킹은 Play 모드에서만 동작합니다.\nPlay 후 손을 추적시키고 원하는 손모양에서 버튼을 누르세요.", MessageType.Info);
        else if (driver == null)
            EditorGUILayout.HelpBox($"{m_Handedness} 손의 XRHandSkeletonDriver를 찾지 못했습니다.\n손을 추적시키면 자동 생성됩니다.", MessageType.Warning);
        else
        {
            int jointCount = driver.jointTransformReferences != null ? driver.jointTransformReferences.Count : 0;
            EditorGUILayout.HelpBox($"드라이버: {driver.name}  (joints: {jointCount})", MessageType.None);
        }

        // ---- HandPoseData 저장 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Record (HandPoseData)", EditorStyles.boldLabel);
        m_SaveFolder = EditorGUILayout.TextField("Save Folder", m_SaveFolder);
        m_PoseName = EditorGUILayout.TextField("Pose Name", m_PoseName);
        m_RecordRootPose = EditorGUILayout.Toggle("Record Root Pose", m_RecordRootPose);
        EditorGUILayout.HelpBox(
            "체크 시: 손가락 모양뿐 아니라 손목(루트)의 로컬 위치·회전까지 HandPoseData에 함께 저장합니다.\n" +
            "저장한 포즈를 적용할 때 손 전체의 배치·방향까지 재현하려면 켜세요.\n" +
            "끄면(기본): 손가락 모양만 저장 — 손목 위치/방향은 적용 대상(부모)에 맞춰집니다.",
            MessageType.None);
        using (new EditorGUI.DisabledScope(driver == null))
            if (GUILayout.Button("● Record Current Pose", GUILayout.Height(30)))
                Record(driver);

        // ---- AnimationClip 내보내기 ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Export AnimationClip (1 frame)", EditorStyles.boldLabel);
        m_ClipFolder = EditorGUILayout.TextField("Clip Folder", m_ClipFolder);
        m_ClipName = EditorGUILayout.TextField("Clip Name", m_ClipName);
        m_ExportRig = (HandPoseAnimationRig)EditorGUILayout.EnumPopup("Export Rig", m_ExportRig);

        using (new EditorGUI.DisabledScope(driver == null))
            if (GUILayout.Button("⬇ Export Current Pose → AnimationClip", GUILayout.Height(30)))
                ExportLiveToClip(driver);

        EditorGUILayout.Space(4);
        m_SourceData = (HandPoseData)EditorGUILayout.ObjectField("From HandPoseData", m_SourceData, typeof(HandPoseData), false);
        using (new EditorGUI.DisabledScope(m_SourceData == null))
            if (GUILayout.Button("⬇ Export HandPoseData → AnimationClip"))
                ExportDataToClip(m_SourceData);

        // ---- 포즈된 XRHand 생성 (프리팹 저장 / 타깃에 부착) ----
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Posed Hand (XRHand Snapshot)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("현재 추적 중인 손의 스켈레톤/메시를 그대로 복제하고 추적 컴포넌트를 제거해\n현재 손모양 그대로의 정적 XRHand로 만듭니다. (고스트핸드 머티리얼 적용)", MessageType.None);
        m_PrefabName = EditorGUILayout.TextField("Hand Name", m_PrefabName);

        EditorGUILayout.Space(2);
        m_AttachTarget = (GameObject)EditorGUILayout.ObjectField("Attach Target", m_AttachTarget, typeof(GameObject), true);
        m_SavePrefabOnAttach = EditorGUILayout.Toggle("Save Prefab on Attach", m_SavePrefabOnAttach);
        using (new EditorGUI.DisabledScope(!m_SavePrefabOnAttach))
            m_PrefabFolder = EditorGUILayout.TextField("Prefab Folder", m_PrefabFolder);
        EditorGUILayout.LabelField(" ", "Attach to Target만 사용 · Save Prefab on Attach 권장 · Clone은 타깃 자식으로 배치", EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(driver == null || m_AttachTarget == null))
            if (GUILayout.Button("➜ Attach to Target (즉시)", GUILayout.Height(28)))
                AttachPosedHand(driver, m_AttachTarget);

        // ---- 지연 캡처 (헤드셋 권장) ----
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("지연 캡처 (헤드셋 권장)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "헤드셋 사용 시 '즉시'로 캡처하면, 마우스로 버튼을 누르는 순간 손이 이미 타깃에서 빠져 위치가 어긋납니다.\n" +
            "지연 캡처: 버튼을 누른 뒤 손을 타깃에 잡고 '그대로 유지'하면, 카운트다운이 끝나는 순간의 손 포즈로 캡처합니다.",
            MessageType.Info);
        m_CaptureDelay = EditorGUILayout.Slider("Capture Delay (s)", m_CaptureDelay, 0.5f, 10f);

        if (m_CapturePending)
        {
            double remain = m_CaptureFireTime - EditorApplication.timeSinceStartup;
            EditorGUILayout.HelpBox($"⏳ {Mathf.Max(0f, (float)remain):0.0}s 후 캡처 — 손을 타깃에 잡고 유지하세요!", MessageType.Warning);
            if (GUILayout.Button("✖ 캡처 취소", GUILayout.Height(24)))
                m_CapturePending = false;
        }
        else
        {
            using (new EditorGUI.DisabledScope(m_AttachTarget == null))
                if (GUILayout.Button($"⏱ Attach to Target ({m_CaptureDelay:0.#}s 후)", GUILayout.Height(34)))
                {
                    if (!Application.isPlaying)
                        Debug.LogWarning("[HandPoseRecorder] 지연 캡처는 Play 모드 + 손 추적 중에만 동작합니다.");
                    else
                    {
                        m_CapturePending = true;
                        m_CaptureFireTime = EditorApplication.timeSinceStartup + m_CaptureDelay;
                    }
                }
        }
    }

    static bool LogValidation(GameObject hand, Transform expectedParent)
    {
        var result = HandPoseSnapshotValidator.Validate(hand, expectedParent);
        if (result.Valid)
        {
            Debug.Log($"[HandPoseRecorder] 검증 OK: {result.Message}", hand);
            return true;
        }

        Debug.LogError($"[HandPoseRecorder] 검증 실패 — 아래 항목을 확인하세요.\n{result.Message}", hand);
        return false;
    }

    // ---------- Posed hand snapshot ----------

    // 라이브 드라이버가 붙은 손 오브젝트를 통째로 복제해 현재 본 포즈를 그대로 굳히고,
    // XR Hands 추적 컴포넌트(드라이버/이벤트/비주얼라이저)를 제거한 정적 XRHand 인스턴스를 만든다.
    GameObject BuildPosedHandClone(XRHandSkeletonDriver driver)
    {
        string handName = string.IsNullOrWhiteSpace(m_PrefabName) ? "HandPose_Snapshot" : m_PrefabName;

        var clone = Instantiate(driver.gameObject);
        clone.name = handName;

        // Instantiate는 원본의 '로컬' transform만 복제하므로, 부모(Hand Visualizer/XR Origin 등)의
        // 월드 오프셋을 잃어버려 스냅샷이 실제 손에서 그 부모 위치만큼 평행이동해 어긋난다.
        // 원본 드라이버 루트의 '월드' pose/scale로 클론 루트를 맞춰, 모든 본이 라이브 손과
        // 동일한 월드 좌표에 오도록 보정한다. (이후 BakePoseRelativeToTarget이 타깃 기준으로 굳힘)
        var src = driver.gameObject.transform;
        clone.transform.SetParent(null, false);
        clone.transform.SetPositionAndRotation(src.position, src.rotation);
        clone.transform.localScale = src.lossyScale;

        StripTrackingComponents(clone);
        RemoveVelocityVisuals(clone);

        // 추적 비주얼라이저가 트래킹 상태에 따라 꺼둘 수 있는 렌더러를 강제로 켜서 정적 스냅샷이 항상 보이게 한다.
        foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;

        ApplyGhostMaterial(clone);
        EnsureSnapshotRoot(clone);

        return clone;
    }

    static void EnsureSnapshotRoot(GameObject hand)
    {
        if (hand.GetComponent<HandPoseSnapshotRoot>() == null)
            hand.AddComponent<HandPoseSnapshotRoot>();
    }

    static Material GetOrCreateGhostMaterial()
    {
        if (s_GhostMaterial != null)
            return s_GhostMaterial;

        s_GhostMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_GhostMaterialPath);
        if (s_GhostMaterial != null)
            return s_GhostMaterial;

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(k_GhostShaderPath);
        if (shader == null)
            shader = Shader.Find(k_GhostShaderName);

        if (shader == null)
        {
            Debug.LogError($"[HandPoseRecorder] 고스트핸드 셰이더를 찾지 못했습니다: {k_GhostShaderPath}");
            return null;
        }

        EnsureFolder("Assets/Materials");
        var mat = new Material(shader) { name = "GhostHand" };
        AssetDatabase.CreateAsset(mat, k_GhostMaterialPath);
        AssetDatabase.SaveAssets();
        s_GhostMaterial = mat;
        return mat;
    }

    static void ApplyGhostMaterial(GameObject root)
    {
        var mat = GetOrCreateGhostMaterial();
        if (mat == null)
            return;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
                shared[i] = mat;
            r.sharedMaterials = shared;
        }
    }

    // 포즈된 XRHand를 생성해 타깃 오브젝트의 자식으로 붙인다.
    // 추적된 손의 월드 포즈를 유지한 채, 타깃 기준 로컬 좌표로 베이크한다(루트 = 0).
    void AttachPosedHand(XRHandSkeletonDriver driver, GameObject target)
    {
        var clone = BuildPosedHandClone(driver);

        Undo.RegisterCreatedObjectUndo(clone, "Attach Posed Hand");

        // 스킨ning이 유효한 상태(scale 1)에서 먼저 BakeMesh — 타깃(0.1 scale)에 붙인 뒤 bake하면 메시가 찌그러짐.
        HandPoseSnapshotMeshBaker.FreezeSkinnedMeshes(clone);
        BakePoseRelativeToTarget(clone.transform, target.transform);
        clone.GetComponent<HandPoseSnapshotRoot>()?.SnapToParentOrigin();

        if (!LogValidation(clone, target.transform))
        {
            EditorGUIUtility.PingObject(clone);
            Selection.activeObject = clone;
            return;
        }

        if (m_SavePrefabOnAttach)
            SavePosedHandPrefabFromInstance(clone);

        EditorGUIUtility.PingObject(clone);
        Selection.activeObject = clone;
        Debug.Log(m_SavePrefabOnAttach
            ? $"[HandPoseRecorder] 포즈된 XRHand를 '{target.name}'에 부착하고 프리팹으로 저장했습니다: {clone.name}"
            : $"[HandPoseRecorder] 포즈된 XRHand를 '{target.name}'의 자식으로 부착했습니다: {clone.name}", clone);
    }

    // 타깃에 붙인 뒤 루트를 (0,0,0)으로 맞춘다.
    // SkinnedMesh: 본은 월드 유지, LeftHand 컨테이너는 (0,0,0).
    // BakeMesh 후 정적 mesh: LeftHand도 월드 위치 유지(vertices가 local에 baked 됨).
    static void BakePoseRelativeToTarget(Transform handRoot, Transform target)
    {
        var all = handRoot.GetComponentsInChildren<Transform>(true);
        var worldPositions = new Vector3[all.Length];
        var worldRotations = new Quaternion[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            worldPositions[i] = all[i].position;
            worldRotations[i] = all[i].rotation;
        }

        handRoot.SetParent(target, false);
        handRoot.localPosition = Vector3.zero;
        handRoot.localRotation = Quaternion.identity;
        handRoot.localScale = Vector3.one;

        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == handRoot)
                continue;

            if (HandPoseSnapshotValidator.IsMeshContainer(t))
            {
                if (IsStaticBakedMeshContainer(t))
                {
                    t.SetPositionAndRotation(worldPositions[i], worldRotations[i]);
                    continue;
                }

                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                continue;
            }

            t.SetPositionAndRotation(worldPositions[i], worldRotations[i]);
        }

        handRoot.GetComponent<HandPoseSnapshotRoot>()?.SetBakeReferenceScale(target);
    }

    static bool IsStaticBakedMeshContainer(Transform t)
        => t.GetComponent<MeshFilter>() != null && t.GetComponent<SkinnedMeshRenderer>() == null;

    static void RemoveVelocityVisuals(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("VelocityPrefab"))
                DestroyImmediate(t.gameObject);
        }
    }

    void SavePosedHandPrefabFromInstance(GameObject instance)
    {
        if (!LogValidation(instance, instance.transform.parent))
            return;

        EnsureFolder(m_PrefabFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{m_PrefabFolder}/{instance.name}.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);

        if (!success || prefab == null)
        {
            Debug.LogError($"[HandPoseRecorder] 프리팹 저장 실패: {path}");
            return;
        }

        var prefabCheck = HandPoseSnapshotValidator.Validate(prefab);
        if (!prefabCheck.Valid)
            Debug.LogError($"[HandPoseRecorder] 저장된 프리팹 검증 실패: {path}\n{prefabCheck.Message}", prefab);

        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(prefab);
        Debug.Log(prefabCheck.Valid
            ? $"[HandPoseRecorder] 포즈 프리팹 저장·검증 완료: {path}"
            : $"[HandPoseRecorder] 포즈 프리팹 저장됨(검증 경고): {path}", prefab);
    }

    static void StripTrackingComponents(GameObject root)
    {
        foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null)
                continue;

            string ns = c.GetType().Namespace;
            if (!string.IsNullOrEmpty(ns) && ns.StartsWith("UnityEngine.XR.Hands"))
                DestroyImmediate(c);
        }
    }

    // ---------- Record ----------

    void Record(XRHandSkeletonDriver driver)
    {
        var refs = driver.jointTransformReferences;
        if (refs == null || refs.Count == 0)
        {
            Debug.LogWarning("[HandPoseRecorder] 드라이버에 관절 참조가 없습니다.");
            return;
        }

        var bones = CollectBonesFromDriver(driver, m_RecordRootPose);
        if (bones.Count == 0)
        {
            Debug.LogWarning("[HandPoseRecorder] 저장할 본 데이터가 없습니다.");
            return;
        }

        var data = CreateInstance<HandPoseData>();
        string fileName = string.IsNullOrWhiteSpace(m_PoseName) ? "HandPose" : m_PoseName;
        data.SetPose(fileName, m_Handedness, bones, normalizedToWristOrigin: !m_RecordRootPose);

        EnsureFolder(m_SaveFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{m_SaveFolder}/{fileName}.asset");
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();

        EditorGUIUtility.PingObject(data);
        Selection.activeObject = data;
        Debug.Log($"[HandPoseRecorder] 포즈 저장 완료: {path}  (bones: {data.Bones.Count})");
    }

    static List<HandBonePoseData> CollectBonesFromDriver(XRHandSkeletonDriver driver, bool useHierarchyLocalPose = false)
    {
        var refs = driver.jointTransformReferences;
        var bones = new List<HandBonePoseData>();
        if (refs == null || refs.Count == 0)
            return bones;

        if (useHierarchyLocalPose)
        {
            foreach (var r in refs)
            {
                var t = r.jointTransform;
                if (t == null)
                    continue;

                bones.Add(new HandBonePoseData
                {
                    jointId = r.xrHandJointID,
                    boneName = t.name,
                    localPosition = t.localPosition,
                    localRotation = t.localRotation,
                });
            }

            return bones;
        }

        var joints = new List<(Transform transform, XRHandJointID jointId)>();
        foreach (var r in refs)
        {
            if (r.jointTransform == null)
                continue;

            joints.Add((r.jointTransform, r.xrHandJointID));
        }

        return HandPoseNormalizer.NormalizeToWristOrigin(joints);
    }

    // ---------- Export to AnimationClip ----------

    // 라이브(추적 중)인 드라이버에서 HandPoseData를 만들고 클립으로 보냅니다.
    void ExportLiveToClip(XRHandSkeletonDriver driver)
    {
        var bones = CollectBonesFromDriver(driver, useHierarchyLocalPose: false);
        if (bones.Count == 0)
        {
            Debug.LogWarning("[HandPoseRecorder] 보낼 본 데이터가 없습니다.");
            return;
        }

        var temp = CreateInstance<HandPoseData>();
        string clipName = string.IsNullOrWhiteSpace(m_ClipName) ? "HandPose_Grip" : m_ClipName;
        temp.SetPose("LiveExport", m_Handedness, bones, normalizedToWristOrigin: true);
        var clip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(temp, clipName, m_ExportRig);
        DestroyImmediate(temp);
        SaveClip(clip, "라이브");
    }

    // 저장된 HandPoseData에서 AnimationClip을 생성합니다.
    void ExportDataToClip(HandPoseData data)
    {
        if (data == null || data.Bones.Count == 0)
        {
            Debug.LogWarning("[HandPoseRecorder] HandPoseData에 본 데이터가 없습니다.");
            return;
        }

        string clipName = string.IsNullOrWhiteSpace(m_ClipName) ? "HandPose_Grip" : m_ClipName;
        var clip = HandPoseAnimationClipBuilder.BuildSinglePoseClip(data, clipName, m_ExportRig);
        SaveClip(clip, data.name);
    }

    void SaveClip(AnimationClip clip, string sourceLabel)
    {
        EnsureFolder(m_ClipFolder);
        string fileName = string.IsNullOrWhiteSpace(m_ClipName) ? "HandPose_Grip" : m_ClipName;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{m_ClipFolder}/{fileName}.anim");
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();

        EditorGUIUtility.PingObject(clip);
        Selection.activeObject = clip;
        Debug.Log($"[HandPoseRecorder] AnimationClip 저장 완료({sourceLabel}): {path}  (curves: {AnimationUtility.GetCurveBindings(clip).Length})");
    }

    // ---------- helpers ----------

    internal static void EnsureFolderPublic(string folder) => EnsureFolder(folder);

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        var parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
