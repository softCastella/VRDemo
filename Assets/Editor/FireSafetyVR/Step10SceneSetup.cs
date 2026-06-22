#if UNITY_EDITOR
using FireSafetyVR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class Step10SceneSetup
{
    const string ScenePath = "Assets/Scenes/Step_10.unity";
    const string HoseMaterialPath = "Assets/Materials/FireHose.mat";

    [MenuItem("Tools/Fire Safety VR/Setup Step_10 Scene")]
    public static void SetupScene()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError($"[Step10] 씬이 없습니다: {ScenePath}");
            return;
        }

        EditorSceneManager.OpenScene(ScenePath);
        var existing = GameObject.Find("FireHydrantSystem");
        if (existing != null)
            Object.DestroyImmediate(existing);

        var fireExisting = GameObject.Find("FireTarget");
        if (fireExisting != null)
            Object.DestroyImmediate(fireExisting);

        var hoseMat = GetOrCreateHoseMaterial();
        var root = new GameObject("FireHydrantSystem");
        root.transform.position = new Vector3(0f, 0f, 2f);

        var system = root.AddComponent<FireHydrantSystem>();

        var box = new GameObject("FireHydrantBox");
        box.transform.SetParent(root.transform, false);
        box.transform.localPosition = Vector3.zero;

        CreatePrimitive(box.transform, "Cube_Back", PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1.2f, 0.35f), new Color(0.8f, 0.1f, 0.1f));
        CreatePrimitive(box.transform, "Cube_Door", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.2f), new Vector3(0.5f, 0.9f, 0.05f), new Color(0.9f, 0.2f, 0.2f));
        CreatePrimitive(box.transform, "Cylinder_Reel", PrimitiveType.Cylinder, new Vector3(0.35f, 1.1f, 0.2f), new Vector3(0.35f, 0.08f, 0.35f), new Color(0.15f, 0.15f, 0.15f));

        var valve = CreatePrimitive(box.transform, "Valve", PrimitiveType.Cylinder, new Vector3(-0.25f, 1.1f, 0.25f), new Vector3(0.12f, 0.04f, 0.12f), new Color(0.9f, 0.75f, 0.1f));
        valve.AddComponent<XRSimpleInteractable>();
        valve.AddComponent<FireHydrantValveInteractable>();

        var hoseStart = new GameObject("HoseStartPoint");
        hoseStart.transform.SetParent(root.transform, false);
        hoseStart.transform.localPosition = new Vector3(0.35f, 1.1f, 0.25f);

        var hoseRendererGo = new GameObject("HoseRenderer");
        hoseRendererGo.transform.SetParent(root.transform, false);
        var line = hoseRendererGo.AddComponent<LineRenderer>();
        line.material = hoseMat;
        line.startWidth = 0.08f;
        line.endWidth = 0.08f;
        line.numCornerVertices = 6;
        line.numCapVertices = 6;
        line.textureMode = LineTextureMode.Tile;
        line.positionCount = 2;
        var simpleHose = hoseRendererGo.AddComponent<SimpleFireHose>();

        var nozzleRoot = new GameObject("Nozzle");
        nozzleRoot.transform.SetParent(root.transform, false);
        nozzleRoot.transform.localPosition = new Vector3(0.8f, 0.9f, 1.2f);

        CreatePrimitive(nozzleRoot.transform, "Cylinder_Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.08f, 0.12f, 0.08f), new Color(0.75f, 0.75f, 0.75f));
        CreatePrimitive(nozzleRoot.transform, "Cylinder_Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), new Vector3(0.05f, 0.06f, 0.05f), new Color(0.2f, 0.2f, 0.2f));
        CreatePrimitive(nozzleRoot.transform, "Cone_Tip", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.18f), new Vector3(0.05f, 0.08f, 0.05f), new Color(0.6f, 0.6f, 0.65f));

        var shootPoint = new GameObject("ShootPoint");
        shootPoint.transform.SetParent(nozzleRoot.transform, false);
        shootPoint.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        shootPoint.transform.localRotation = Quaternion.identity;

        var waterGo = new GameObject("WaterParticle");
        waterGo.transform.SetParent(nozzleRoot.transform, false);
        waterGo.transform.localPosition = shootPoint.transform.localPosition;
        waterGo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        var water = waterGo.AddComponent<ParticleSystem>();
        ConfigureWaterParticle(water);

        var nozzleRb = nozzleRoot.AddComponent<Rigidbody>();
        nozzleRb.mass = 0.5f;
        nozzleRb.linearDamping = 0.2f;
        nozzleRb.angularDamping = 0.5f;
        nozzleRb.interpolation = RigidbodyInterpolation.Interpolate;
        nozzleRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var nozzleCollider = nozzleRoot.AddComponent<CapsuleCollider>();
        nozzleCollider.height = 0.35f;
        nozzleCollider.radius = 0.06f;
        nozzleCollider.direction = 2;

        var grab = nozzleRoot.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = false;

        var nozzle = nozzleRoot.AddComponent<FireHoseNozzle>();
        var bridge = nozzleRoot.AddComponent<FireHoseNozzleTriggerBridge>();

        var hoseSo = new SerializedObject(simpleHose);
        hoseSo.FindProperty("m_HoseStart").objectReferenceValue = hoseStart.transform;
        hoseSo.FindProperty("m_Nozzle").objectReferenceValue = nozzleRoot.transform;
        hoseSo.FindProperty("m_LineRenderer").objectReferenceValue = line;
        hoseSo.ApplyModifiedPropertiesWithoutUndo();

        var nozzleSo = new SerializedObject(nozzle);
        nozzleSo.FindProperty("m_ShootPoint").objectReferenceValue = shootPoint.transform;
        nozzleSo.FindProperty("m_WaterParticle").objectReferenceValue = water;
        nozzleSo.ApplyModifiedPropertiesWithoutUndo();

        var systemSo = new SerializedObject(system);
        systemSo.FindProperty("m_Nozzle").objectReferenceValue = nozzle;
        systemSo.ApplyModifiedPropertiesWithoutUndo();

        var bridgeSo = new SerializedObject(bridge);
        bridgeSo.FindProperty("m_System").objectReferenceValue = system;
        bridgeSo.ApplyModifiedPropertiesWithoutUndo();

        var valveSo = new SerializedObject(valve.GetComponent<FireHydrantValveInteractable>());
        valveSo.FindProperty("m_System").objectReferenceValue = system;
        valveSo.ApplyModifiedPropertiesWithoutUndo();

        var floor = GameObject.Find("Floor") ?? GameObject.Find("Plane");
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
        }

        var fire = new GameObject("FireTarget");
        fire.transform.position = new Vector3(0f, 0.5f, 4.5f);
        var fireVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fireVisual.name = "FireVisual";
        fireVisual.transform.SetParent(fire.transform, false);
        fireVisual.transform.localPosition = Vector3.up * 0.5f;
        fireVisual.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        Object.DestroyImmediate(fireVisual.GetComponent<Collider>());

        var fireCollider = fire.AddComponent<CapsuleCollider>();
        fireCollider.height = 1.5f;
        fireCollider.radius = 0.5f;
        fireCollider.center = new Vector3(0f, 0.75f, 0f);

        var fireParticleGo = new GameObject("FireParticle");
        fireParticleGo.transform.SetParent(fire.transform, false);
        fireParticleGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        var firePs = fireParticleGo.AddComponent<ParticleSystem>();
        ConfigureFireParticle(firePs);

        var fireTarget = fire.AddComponent<FireTarget>();
        var fireSo = new SerializedObject(fireTarget);
        fireSo.FindProperty("m_FireParticle").objectReferenceValue = firePs;
        fireSo.FindProperty("m_FireVisualRoot").objectReferenceValue = fireVisual;
        fireSo.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            var mgr = new GameObject("XR Interaction Manager");
            mgr.AddComponent<XRInteractionManager>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[Step10] FireHydrantSystem 씬 구성 완료.");
    }

    static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }

        return go;
    }

    static Material GetOrCreateHoseMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HoseMaterialPath);
        if (mat != null)
            return mat;

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        mat = new Material(shader) { color = new Color(0.55f, 0.05f, 0.05f) };
        AssetDatabase.CreateAsset(mat, HoseMaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void ConfigureWaterParticle(ParticleSystem ps)
    {
        var main = ps.main;
        main.startSpeed = 12f;
        main.startLifetime = 0.35f;
        main.startSize = 0.06f;
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.4f, 0.7f, 1f, 0.85f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 4f;
        shape.radius = 0.02f;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static void ConfigureFireParticle(ParticleSystem ps)
    {
        var main = ps.main;
        main.startSpeed = 2f;
        main.startLifetime = 1.2f;
        main.startSize = 0.4f;
        main.maxParticles = 80;
        main.startColor = new Color(1f, 0.45f, 0.05f, 0.9f);

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        ps.Play();
    }
}
#endif
