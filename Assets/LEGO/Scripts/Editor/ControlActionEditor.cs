using Unity.LEGO.Behaviours;
using UnityEditor;

namespace Unity.LEGO.EditorExt
{
    [CustomEditor(typeof(ControlAction), true)]
    public class VehicleActionEditor : MovementActionEditor
    {
        SerializedProperty m_ControlTypeProp;
        SerializedProperty m_InputTypeProp;
        SerializedProperty m_SpeedProp;
        SerializedProperty m_RotateSpeedProp;
        SerializedProperty m_IsPlayerProp;
        SerializedProperty m_AlwaysMovingForwardProp;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_ControlTypeProp = serializedObject.FindProperty("m_ControlType");
            m_InputTypeProp = serializedObject.FindProperty("m_InputType");
            m_SpeedProp = serializedObject.FindProperty("m_Speed");
            m_RotateSpeedProp = serializedObject.FindProperty("m_RotationSpeed");
            m_IsPlayerProp = serializedObject.FindProperty("m_IsPlayer");
            m_AlwaysMovingForwardProp = serializedObject.FindProperty("m_AlwaysMovingForward");
        }

        protected override void CreateGUI() 
        {
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            // EditorGUILayout.PropertyField(m_ControlTypeProp);

            EditorGUILayout.PropertyField(m_InputTypeProp);

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(m_SpeedProp);

            EditorGUILayout.PropertyField(m_RotateSpeedProp);

            // EditorGUILayout.PropertyField(m_AlwaysMovingForwardProp);

            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            EditorGUILayout.PropertyField(m_IsPlayerProp);

            EditorGUILayout.PropertyField(m_CollideProp);

            EditorGUI.EndDisabledGroup();
        }
    }
}
