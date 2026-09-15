using TunnelCrew.Presentation.Prototype;
using UnityEditor;
using UnityEngine;

namespace TunnelCrew.EditorTools
{
    [CustomEditor(typeof(ModularGunnerCameraRig))]
    public sealed class ModularGunnerCameraRigEditor : UnityEditor.Editor
    {
        UnityEditor.Editor _profileEditor;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_profile"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_target"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetBody"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetController"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_arenaMin"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_arenaMax"));
            serializedObject.ApplyModifiedProperties();

            var rig = (ModularGunnerCameraRig)target;
            if (rig.Profile == null) return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "아래 값은 Camera Profile 에셋에 직접 저장됩니다. Play Mode에서 조절한 값도 종료 후 유지됩니다.",
                MessageType.Info);
            Editor.CreateCachedEditor(rig.Profile, null, ref _profileEditor);
            _profileEditor.OnInspectorGUI();
            if (GUI.changed)
            {
                EditorUtility.SetDirty(rig.Profile);
                AssetDatabase.SaveAssetIfDirty(rig.Profile);
            }
        }

        void OnDisable()
        {
            if (_profileEditor != null) DestroyImmediate(_profileEditor);
        }
    }
}
