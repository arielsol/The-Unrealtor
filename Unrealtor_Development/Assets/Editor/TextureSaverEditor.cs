using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TextureSaver))]
public class TextureSaverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // shows regular fields

        if (Application.isPlaying) 
        {
            TextureSaver saver = (TextureSaver)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Save RenderTextures to PNG"))
            {
                saver.SaveRenderTextures();
            }
        }
    }
}
