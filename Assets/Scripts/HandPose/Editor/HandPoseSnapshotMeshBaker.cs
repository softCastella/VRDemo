using UnityEngine;

/// <summary>
/// 스냅샷 시 SkinnedMeshRenderer를 현재 포즈의 정적 Mesh로 굳힙니다.
/// 타깃(비균일 스케일)에 부모로 붙이기 전에 호출해야 메시가 찌그러지지 않습니다.
/// </summary>
public static class HandPoseSnapshotMeshBaker
{
    public static void FreezeSkinnedMeshes(GameObject root)
    {
        if (root == null)
            return;

        var skinnedMeshes = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var skinnedMesh in skinnedMeshes)
        {
            if (skinnedMesh == null)
                continue;

            BakeSkinnedMeshRenderer(skinnedMesh);
        }
    }

    static void BakeSkinnedMeshRenderer(SkinnedMeshRenderer skinnedMesh)
    {
        var sourceMesh = skinnedMesh.sharedMesh;
        var bakedMesh = new Mesh
        {
            name = sourceMesh != null ? $"{sourceMesh.name}_Baked" : "HandPose_BakedMesh"
        };
        skinnedMesh.BakeMesh(bakedMesh);

        var go = skinnedMesh.gameObject;
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = bakedMesh;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = skinnedMesh.sharedMaterials;
        renderer.enabled = skinnedMesh.enabled;

        Object.DestroyImmediate(skinnedMesh);
    }
}
