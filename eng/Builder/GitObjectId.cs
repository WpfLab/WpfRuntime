namespace WpfReorganize.Builder;

internal readonly record struct GitObjectId
{
    private GitObjectId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Short => Value[..12];

    public static GitObjectId Parse(string value)
    {
        if (!TryParse(value, out var objectId))
        {
            throw new ArgumentException(BuilderResources.InvalidGitObjectId, nameof(value));
        }

        return objectId;
    }

    public static bool TryParse(string? value, out GitObjectId objectId)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 40
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            objectId = default;
            return false;
        }

        objectId = new GitObjectId(value.ToLowerInvariant());
        return true;
    }

    public override string ToString() => Value;
}
