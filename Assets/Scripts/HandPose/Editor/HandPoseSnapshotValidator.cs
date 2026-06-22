using System.Collections.Generic;
using System.Text;
using UnityEngine;

public readonly struct HandPoseSnapshotValidationResult
{
    public HandPoseSnapshotValidationResult(bool valid, string message)
    {
        Valid = valid;
        Message = message;
    }

    public bool Valid { get; }
    public string Message { get; }
}

public static class HandPoseSnapshotValidator
{
    public static HandPoseSnapshotValidationResult Validate(GameObject hand)
    {
        return Validate(hand, null);
    }

    public static HandPoseSnapshotValidationResult Validate(GameObject hand, Transform expectedParent)
    {
        if (hand == null)
            return new HandPoseSnapshotValidationResult(false, "손 오브젝트가 null입니다.");

        var issues = new List<string>();

        if (hand.GetComponent<HandPoseSnapshotRoot>() == null)
            issues.Add("HandPoseSnapshotRoot 컴포넌트가 없습니다.");

        if (expectedParent != null && hand.transform.parent != expectedParent)
            issues.Add($"부모가 '{expectedParent.name}'가 아닙니다 (현재: '{(hand.transform.parent != null ? hand.transform.parent.name : "none")}').");

        if (!IsNearOrigin(hand.transform.localPosition) || !IsNearIdentity(hand.transform.localRotation))
            issues.Add("루트 로컬 변환이 원점이 아닙니다.");

        foreach (var behaviour in hand.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            var typeNamespace = behaviour.GetType().Namespace;
            if (!string.IsNullOrEmpty(typeNamespace) && typeNamespace.StartsWith("UnityEngine.XR.Hands"))
                issues.Add($"추적 컴포넌트가 남아 있습니다: {behaviour.GetType().Name}");
        }

        foreach (var meshContainer in FindMeshContainers(hand.transform))
        {
            if (!IsNearOrigin(meshContainer.localPosition) || !IsNearIdentity(meshContainer.localRotation))
                issues.Add($"메시 컨테이너 '{GetTransformPath(meshContainer)}'가 루트 원점이 아닙니다.");
        }

        var renderers = hand.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            issues.Add("Renderer가 없습니다.");
        else
        {
            var disabledCount = 0;
            foreach (var renderer in renderers)
            {
                if (renderer != null && !renderer.enabled)
                    disabledCount++;
            }

            if (disabledCount == renderers.Length)
                issues.Add("모든 Renderer가 비활성화되어 있습니다.");
        }

        if (issues.Count == 0)
            return new HandPoseSnapshotValidationResult(true, "스냅샷 구조가 유효합니다.");

        var message = new StringBuilder();
        for (var i = 0; i < issues.Count; i++)
            message.AppendLine($"- {issues[i]}");

        return new HandPoseSnapshotValidationResult(false, message.ToString().TrimEnd());
    }

    public static bool IsMeshContainer(Transform transform)
    {
        if (transform == null)
            return false;

        var name = transform.name;
        return name == "LeftHand" || name == "RightHand";
    }

    static IEnumerable<Transform> FindMeshContainers(Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (IsMeshContainer(transform))
                yield return transform;
        }
    }

    static bool IsNearOrigin(Vector3 value)
    {
        return value.sqrMagnitude <= 1e-8f;
    }

    static bool IsNearIdentity(Quaternion value)
    {
        return Quaternion.Angle(value, Quaternion.identity) <= 0.1f;
    }

    static string GetTransformPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        var path = transform.name;
        var parent = transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }
}
