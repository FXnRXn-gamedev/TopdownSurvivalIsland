using UnityEngine;

public class ShowIfAttribute : PropertyAttribute
{
	public string ConditionFieldName { get; }
	public object CompareValue { get; }
	public bool IsEnumComparison { get; }

	// Constructor for boolean conditions
	public ShowIfAttribute(string conditionFieldName, bool value)
	{
		ConditionFieldName = conditionFieldName;
		CompareValue = value;
		IsEnumComparison = false;
	}

	// Constructor for enum conditions
	public ShowIfAttribute(string conditionFieldName, object enumValue)
	{
		ConditionFieldName = conditionFieldName;
		CompareValue = enumValue;
		IsEnumComparison = true;
	}
}