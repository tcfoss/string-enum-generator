using Microsoft.CodeAnalysis;
using TcfOss.DataStructures.ValueCollections;

namespace TcfOss.StringEnumGenerator;

public readonly record struct StringEnumOption(
    string Text,
    string VariableName,
    int Order,
    ValueArray<string> OtherValues = default,
    DiagnosticDescriptor? Error = null,
    ValueArray<string>? ErrorArgs = null)
{ }
