using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RowanBridge;

/// <summary>
/// Represents a read-only collection of settings shared between the strategy and the UI.
/// </summary>
public sealed class RowanSettings
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    private RowanSettings(IReadOnlyDictionary<string, object?> values)
    {
        _values = values;
    }

    public static RowanSettings Empty { get; } = new RowanSettings(
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>()));

    public IReadOnlyDictionary<string, object?> Values => _values;

    public static RowanSettings Create(IDictionary<string, object?> values)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));

        return new RowanSettings(new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(values)));
    }

    public T? GetValue<T>(string key, T? defaultValue = default)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (_values.TryGetValue(key, out var raw) && raw is T typed)
            return typed;

        if (_values.TryGetValue(key, out raw) && raw is IConvertible && typeof(T).IsEnum)
        {
            try
            {
                return (T)Enum.Parse(typeof(T), raw.ToString() ?? string.Empty, ignoreCase: true);
            }
            catch
            {
                // ignore parse errors and fall back to default value.
            }
        }

        return defaultValue;
    }

    public RowanSettings WithValue(string key, object? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Setting key cannot be null or whitespace.", nameof(key));

        var copy = new Dictionary<string, object?>(_values) { [key] = value };
        return new RowanSettings(new ReadOnlyDictionary<string, object?>(copy));
    }

    public RowanSettings Without(string key)
    {
        if (!_values.ContainsKey(key))
            return this;

        var copy = new Dictionary<string, object?>(_values);
        copy.Remove(key);
        return new RowanSettings(new ReadOnlyDictionary<string, object?>(copy));
    }
}
