# String Enum Generator

This project is a source generator which writes the implementing code for a particular
sort of "enum-with-behavior."

## Dependencies

This package and all consumers of it must reference the `TcfOss.DataStructures` package, which
defines the `IStringEnum` interface which the generated code implements, as well as the
`ValueArray` type, which is part of its contract.

## Basic Example

To indicate that a class should serve as the basis for an `IStringEnum`, the class should
1. be `partial`, and
2. be decorated with one or more `StringEnumAttribute`s, one for each allowed value, like so.

Here is a simple example:

```cs
using TcfOss.StringEnumGenerator;

[StringEnum("RESTRICT")]
[StringEnum("CASCADE")]
[StringEnum("SET NULL")]
[StringEnum("NO ACTION")]
[StringEnum("SET DEFAULT")]
public partial class ReferentialAction { }
```

This will trigger `StringEnumGenerator` to generate the following implementation:

```cs
public sealed partial class ReferentialAction : IStringEnum<ReferentialAction>, IEquatable<ReferentialAction>, IComparable<ReferentialAction>, IComparable
{
    private static ValueArray<string> s_allowedValues = new ValueArray<string>(new string[] {
        "RESTRICT",
        "CASCADE",
        "SET NULL",
        "NO ACTION",
        "SET DEFAULT",
    });

    public static ValueArray<string> AllowedValues => s_allowedValues;

    private readonly string _text;
    private readonly int _order;

    public string Text => _text;
    public int Order => _order;

    private ReferentialAction(string text, int order)
    {
        _text = text;
        _order = order;
    }

    public override string ToString()
    {
        return _text;
    }

    public static ReferentialAction Parse(string text)
    {
        return text.ToUpperInvariant() switch
        {
            "RESTRICT" => Restrict,
            "CASCADE" => Cascade,
            "SET NULL" => SetNull,
            "NO ACTION" => NoAction,
            "SET DEFAULT" => SetDefault,
            _ => throw new ArgumentException($"Unknown ReferentialAction: {text}")
        };
    }

    public static bool TryParse(string text, [NotNullWhen(true)] out ReferentialAction? value)
    {
        try
        {
            value = Parse(text);
            return true;
        }
        catch (ArgumentException)
        {
            value = null;
            return false;
        }
    }

    private bool Equals(ReferentialAction? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

    bool IEquatable<ReferentialAction>.Equals(ReferentialAction? other) => Equals(other);

    public static bool operator ==(ReferentialAction? left, ReferentialAction? right) => ReferenceEquals(left, right);
    public static bool operator !=(ReferentialAction? left, ReferentialAction? right) => !ReferenceEquals(left, right);

    public int CompareTo(ReferentialAction? other) => (other is null) ? 1 : _order.CompareTo(other._order);

    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        ReferentialAction other => CompareTo(other),
        _ => throw new ArgumentException("Object must be of type ReferentialAction.", nameof(obj))
    };

    public static bool operator <(ReferentialAction? left, ReferentialAction? right) => left is null ? right is not null : left.CompareTo(right) < 0;
    public static bool operator <=(ReferentialAction? left, ReferentialAction? right) => left is null || left.CompareTo(right) <= 0;
    public static bool operator >(ReferentialAction? left, ReferentialAction? right) => left is not null && left.CompareTo(right) > 0;
    public static bool operator >=(ReferentialAction? left, ReferentialAction? right) => left is null ? right is null : left.CompareTo(right) >= 0;

    public static readonly ReferentialAction Restrict = new("RESTRICT", 0);
    public static readonly ReferentialAction Cascade = new("CASCADE", 1);
    public static readonly ReferentialAction SetNull = new("SET NULL", 2);
    public static readonly ReferentialAction NoAction = new("NO ACTION", 3);
    public static readonly ReferentialAction SetDefault = new("SET DEFAULT", 4);
}
```

## Ordering and Comparison

Generated types implement `IComparable<T>` and `IComparable`, and support `<`, `<=`, `>`, and `>=`.
Each value exposes an integer `Order`, starting at zero.
Use the named attribute property `Order` to override a value; subsequent implicit values
increment from that override:

```cs
[StringEnum("LOW")]
[StringEnum("MEDIUM", Order = 10)]
[StringEnum("HIGH")]
public partial class Priority { }
```

Here, the orders are `0`, `10`, and `11`. Negative orders and explicit zero are supported. Orders
must be unique.

Equality remains reference-based.

## Special Case: Enumerated Variable Names

If the string representation of the enum-like object is not a valid C# identifier, the variable
name can be specified. For example:
```cs
[StringEnum("+", "Plus")]
[StringEnum("-", variableName: "Minus")]
public partial class ReferentialAction { }
```

Then the generated code will contain
```cs
public sealed partial class ReferentialAction : IStringEnum<ReferentialAction>, IEquatable<ReferentialAction>, IComparable<ReferentialAction>, IComparable
{

    // Other members omitted for brevity

    public static ReferentialAction Parse(string text)
    {
        return text.ToUpperInvariant() switch
        {
            "+" => Plus,
            "-" => Minus,
            _ => throw new ArgumentException($"Unknown ReferentialAction: {text}")
        };
    }

    // Other members omitted for brevity

    public static readonly ReferentialAction Plus = new("+", 0);
    public static readonly ReferentialAction Minus = new("-", 1);
}
```

## Special Case: Equivalent Values

If multiple textual values are semantically equivalent and, therefore, should be mapped to
the same enumeration value, that can be specified via subsequent arguments to the
attribute:
```cs
[StringEnum("IN")]
[StringEnum("OUT")]
[StringEnum("INOUT", "InOut", "IN OUT", "IN_OUT")]
public sealed partial class RoutineParameterDirection { }
```

The generated code is
```cs
public sealed partial class RoutineParameterDirection
{
    private static ValueArray<string> s_allowedValues = new ValueArray<string>(new string[] {
        "IN",
        "OUT",
        "INOUT",
        "IN OUT",
        "IN_OUT",
    });

    // Other members omitted for brevity

    public static RoutineParameterDirection Parse(string text)
    {
        return text.ToUpperInvariant() switch
        {
            "IN" => In,
            "OUT" => Out,
            "INOUT" => InOut,
            "IN OUT" => InOut,
            "IN_OUT" => InOut,
            _ => throw new ArgumentException($"Unknown RoutineParameterDirection: {text}")
        };
    }

    // Other members omitted for brevity

    public static readonly RoutineParameterDirection In = new("IN", 0);
    public static readonly RoutineParameterDirection Out = new("OUT", 1);
    public static readonly RoutineParameterDirection InOut = new("INOUT", 2);
}
```

## Reporting Issues and Contributing

Issues for this project are tracked on [IssueTracker](https://issues.tcflanagan.net/string-enum-generator). If you encounter any bugs or have feature requests, please submit them there.

Contributions are welcome. Follow the usual fork-and-pull request workflow. Before submitting a pull request, make sure

1. Code is linted: run `dotnet format --severity info --verify-no-changes` in the repo root.
2. All existing tests succeed.
3. Any new code includes appropriate tests.
4. Documentation is updated as necessary (and—especially if AI generates the updates—spaces are removed around any em-dashes).

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
