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
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 40
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(BuilderResources.InvalidGitObjectId, nameof(value));
        }

        return new GitObjectId(value.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
