namespace LantanaGroup.Link.PatientFlowSimulator;

/// <summary>
/// Minimal argument parsing: a command word followed by --option value pairs and --flags.
/// </summary>
public sealed class CommandLineArguments
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);

    private CommandLineArguments(string? command)
    {
        Command = command;
    }

    /// <summary>
    /// The command word, or null when none was given.
    /// </summary>
    public string? Command { get; }

    /// <summary>
    /// Parses the raw arguments.
    /// </summary>
    public static CommandLineArguments Parse(string[] args)
    {
        string? command = null;
        var index = 0;
        if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
        {
            command = args[0];
            index = 1;
        }

        var result = new CommandLineArguments(command);
        while (index < args.Length)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            var name = token[2..];
            var equals = name.IndexOf('=');
            if (equals >= 0)
            {
                result._options[name[..equals]] = name[(equals + 1)..];
                index++;
                continue;
            }

            var hasValue = index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal);
            result._options[name] = hasValue ? args[index + 1] : "true";
            index += hasValue ? 2 : 1;
        }

        return result;
    }

    /// <summary>
    /// An option's value, or the environment variable, or the default.
    /// </summary>
    public string? Get(string name, string? environmentVariable = null, string? defaultValue = null)
    {
        if (_options.TryGetValue(name, out var value))
        {
            return value;
        }

        if (environmentVariable != null)
        {
            var fromEnvironment = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// An option that must be present.
    /// </summary>
    public string Require(string name, string? environmentVariable = null)
    {
        return Get(name, environmentVariable)
               ?? throw new ArgumentException(
                   $"--{name} is required" + (environmentVariable != null ? $" (or set {environmentVariable})" : "") + ".");
    }

    /// <summary>
    /// True when the flag was given.
    /// </summary>
    public bool Has(string name) => _options.ContainsKey(name);
}
