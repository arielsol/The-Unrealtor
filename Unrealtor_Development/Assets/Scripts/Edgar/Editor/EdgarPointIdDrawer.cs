using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(EdgarPointIdAttribute))]
public class EdgarPointIdDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        EdgarManager manager = Object.FindFirstObjectByType<EdgarManager>();
        List<string> pointIds = new List<string> { "(None)" };

        if (manager != null && manager.edgarPointsHolder != null)
        {
            EdgarPoint[] points = manager.edgarPointsHolder.GetComponentsInChildren<EdgarPoint>(true);
            foreach (EdgarPoint point in points)
            {
                if (point != null && !string.IsNullOrEmpty(point.pointId))
                {
                    pointIds.Add(point.pointId);
                }
            }
        }

        string currentValue = property.stringValue;
        int currentIndex = pointIds.IndexOf(currentValue);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        Rect popupPosition = EditorGUI.PrefixLabel(position, label);
        int newIndex = EditorGUI.Popup(popupPosition, currentIndex, pointIds.ToArray());

        if (newIndex != currentIndex)
        {
            if (newIndex == 0)
            {
                property.stringValue = "";
            }
            else
            {
                property.stringValue = pointIds[newIndex];
            }
        }

        EditorGUI.EndProperty();
    }
}

