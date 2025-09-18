using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using XericUI.OverrideLayout;

namespace XericUIEditor.OverrideLayout
{
    [CustomEditor(typeof(XericHorizontalOrVerticalLayoutGroup), true)]
    [CanEditMultipleObjects]
    public class XericHorizontalOrVerticalLayoutGroupEditor : HorizontalOrVerticalLayoutGroupEditor
    {
        SerializedProperty ignoreInvisible;
        SerializedProperty enableListScreening;
        SerializedProperty blackListMode;
        SerializedProperty whiteList_list;
        SerializedProperty blackList_list;

        protected override void OnEnable()
        {
            base.OnEnable();
            ignoreInvisible = serializedObject.FindProperty("layoutIgnoreInactive");
            enableListScreening = serializedObject.FindProperty("enableListScreening");
            blackListMode = serializedObject.FindProperty("blackListMode");
            whiteList_list = serializedObject.FindProperty("m_whiteList_list");
            blackList_list = serializedObject.FindProperty("m_blackList_list");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            EditorGUILayout.PropertyField(enableListScreening);
            if (enableListScreening.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(blackListMode,
                    EditorGUIUtility.TrTextContent(blackListMode.boolValue ? "Black List Mode" : "White List Mode"));
                if (blackListMode.boolValue)
                    EditorGUILayout.PropertyField(blackList_list, EditorGUIUtility.TrTextContent("Black List"));
                else
                    EditorGUILayout.PropertyField(whiteList_list, EditorGUIUtility.TrTextContent("White List"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(ignoreInvisible, true);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}