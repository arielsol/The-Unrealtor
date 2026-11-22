using UnityEditor;
using UnityEngine;

public static class RemoveCollidersEditor
{
    [MenuItem("Tools/Remove Colliders from Selection %#r")]
    static void RemoveColliders()
    {
        var selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            Debug.LogWarning("No objects selected.");
            return;
        }

        int count = 0;
        Undo.SetCurrentGroupName("Remove Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var go in selected)
        {
            var colliders = go.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                Undo.DestroyObjectImmediate(col);
                count++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Removed {count} collider(s) from {selected.Length} object(s).");
    }

    [MenuItem("Tools/Remove Colliders from Selection %#r", true)]
    static bool Validate() => Selection.gameObjects.Length > 0;
}
