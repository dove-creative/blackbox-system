#if UNITY_5_3_OR_NEWER
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BlackThunder.BlackboxSystem.Exporters
{
    public class BlackboxLogExporter : MonoBehaviour
    {
        [SerializeField] private Object _target;
        [SerializeField] private bool _quitOnExport = true;

#if UNITY_EDITOR
        [CustomEditor(typeof(BlackboxLogExporter))]
        private class BlackboxLogExporterEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                var target = (BlackboxLogExporter)base.target;
                var subject = target._target;

                GUILayout.Space(8);

                if (subject == null)
                {
                    EditorGUILayout.HelpBox("Target must be assigned to enable Log Exporter", MessageType.Warning);
                    return;
                }

                if (Application.isPlaying)
                {
                    if (GUILayout.Button("Export Logs"))
                    {
                        BlackboxHandle.Of(subject).Export();
                        if (target._quitOnExport) EditorApplication.ExitPlaymode();
                    }
                }
                else
                    GUILayout.Label("Enter play mode to export logs", EditorStyles.centeredGreyMiniLabel);
            }
        }
#endif
    }
}
#endif
