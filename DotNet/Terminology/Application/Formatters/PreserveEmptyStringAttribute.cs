namespace LantanaGroup.Link.Terminology.Application.Formatters;

/// <summary>
/// Marks a bound parameter whose empty string must reach the action as an empty string rather than as null.
/// </summary>
/// <remarks>
/// Apply this only where an omitted parameter and a blank one mean different things and the action has to
/// tell them apart. MVC's default is <c>ConvertEmptyStringToNull = true</c>, which collapses both into null
/// before the action runs; <see cref="PreserveEmptyStringMetadataProvider"/> honours this attribute by
/// turning that conversion off for the marked parameter only.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PreserveEmptyStringAttribute : Attribute
{
}
