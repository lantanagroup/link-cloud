namespace LantanaGroup.Automation.Generation;

public static class DeterministicHash
{
    public static uint GetStableHash32(this string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;
            foreach (var c in value)
            {
                hash ^= c;
                hash *= prime;
            }

            return hash;
        }
    }
}
