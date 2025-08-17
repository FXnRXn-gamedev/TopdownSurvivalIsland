#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

[CustomPropertyDrawer(typeof(ShowIfAttribute))]
public partial class ShowIfAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        bool shouldShow = ShouldShow(property, showIf);

        if (shouldShow)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ShowIfAttribute showIf = (ShowIfAttribute)attribute;
        bool shouldShow = ShouldShow(property, showIf);

        if (shouldShow)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
        
        // Collapse the hidden field
        return -EditorGUIUtility.standardVerticalSpacing;
    }

    private bool ShouldShow(SerializedProperty property, ShowIfAttribute showIf)
    {
        SerializedProperty conditionProperty = GetConditionProperty(property, showIf.ConditionFieldName);
        
        if (conditionProperty == null)
        {
            Debug.LogWarning($"Condition field '{showIf.ConditionFieldName}' not found");
            return true;
        }

        if (showIf.IsEnumComparison)
        {
            return HandleEnumCondition(conditionProperty, showIf.CompareValue);
        }
        else
        {
            return HandleBoolCondition(conditionProperty, showIf.CompareValue);
        }
    }

    private SerializedProperty GetConditionProperty(SerializedProperty property, string conditionFieldName)
    {
        string propertyPath = property.propertyPath;
        string conditionPath = propertyPath.Replace(property.name, conditionFieldName);
        return property.serializedObject.FindProperty(conditionPath);
    }

    private bool HandleBoolCondition(SerializedProperty conditionProperty, object compareValue)
    {
        if (conditionProperty.propertyType == SerializedPropertyType.Boolean)
        {
            return conditionProperty.boolValue == (bool)compareValue;
        }
        
        Debug.LogWarning("ShowIf requires a boolean field for this comparison");
        return true;
    }

    private bool HandleEnumCondition(SerializedProperty conditionProperty, object compareValue)
    {
        if (conditionProperty.propertyType == SerializedPropertyType.Enum)
        {
            int currentValue = conditionProperty.enumValueIndex;
            int targetValue = (int)compareValue;
            return currentValue == targetValue;
        }
        
        Debug.LogWarning("ShowIf requires an enum field for this comparison");
        return true;
    }
}
#endif