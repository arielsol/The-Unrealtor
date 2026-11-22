using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(EdgarPoint))]
[CanEditMultipleObjects]
public class EdgarPointEditor : Editor
{
    SerializedProperty pointIdProp;

    void OnEnable()
    {
        pointIdProp = serializedObject.FindProperty("pointId");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(pointIdProp);

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();

            // Update GameObject name to match pointId
            foreach (Object targetObj in targets)
            {
                EdgarPoint point = targetObj as EdgarPoint;
                if (point != null && !string.IsNullOrEmpty(point.pointId))
                {
                    point.gameObject.name = point.pointId;
                    EditorUtility.SetDirty(point.gameObject);
                }
            }
        }

        // Draw remaining properties
        DrawPropertiesExcluding(serializedObject, "pointId");

        serializedObject.ApplyModifiedProperties();
    }
}

