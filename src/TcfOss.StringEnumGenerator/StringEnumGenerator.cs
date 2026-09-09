using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TcfOss.DataStructures.ValueCollections;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "RoslynDiagnostics",
    "RS1041:Do not use obsoleted APIs",
    Justification = "This extension is too project-specific to care about this.")]

namespace TcfOss.StringEnumGenerator;

[Generator]
public class StringEnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // find class declarations that have [StringEnum(...)] attribute
        IncrementalValuesProvider<ClassDeclarationSyntax> classesWithAttribute = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) =>
                {
                    if (node is not ClassDeclarationSyntax cds)
                    {
                        return false;
                    }

                    foreach (AttributeListSyntax list in cds.AttributeLists)
                    {
                        foreach (AttributeSyntax attr in list.Attributes)
                        {
                            string n = attr.Name.ToString();
                            if (n == "StringEnum" || n == "StringEnumAttribute" || n.EndsWith(".StringEnum", StringComparison.Ordinal) || n.EndsWith(".StringEnumAttribute", StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                },
                (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(d => d != null);

        IncrementalValueProvider<ImmutableArray<ClassDeclarationSyntax>> collected = classesWithAttribute.Collect();
        IncrementalValueProvider<(ImmutableArray<ClassDeclarationSyntax> Left, Compilation Right)> compilationAndClasses = collected.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(compilationAndClasses, (sourceProductionContext, classArrayAndCompilation) =>
        {
            ImmutableArray<ClassDeclarationSyntax> classesArray = classArrayAndCompilation.Left;
            Compilation compilation = classArrayAndCompilation.Right;

            foreach (ClassDeclarationSyntax classDecl in classesArray)
            {
                SemanticModel model = compilation.GetSemanticModel(classDecl.SyntaxTree);
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                IEnumerable<AttributeData> attrs = symbol.GetAttributes().Where(a => a.AttributeClass?.Name == "StringEnumAttribute");

                var values = new List<StringEnumOption>();
                bool hasError = false;
                foreach (AttributeData attr in attrs)
                {
                    var curr = new List<string>();
                    foreach (TypedConstant arg in attr.ConstructorArguments)
                    {
                        if (arg.Kind == TypedConstantKind.Array)
                        {
                            foreach (TypedConstant tc in arg.Values)
                            {
                                if (tc.Value is string s)
                                {
                                    curr.Add(s);
                                }
                            }
                        }
                        else if (arg.Value is string s)
                        {
                            curr.Add(s);
                        }
                    }
                    StringEnumOption option = ToStringEnumOption(curr);

                    if (option.Error != null)
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(option.Error, classDecl.GetLocation(), [.. option.ErrorArgs ?? []]));

                        hasError = true;
                        break;
                    }
                    else
                    {
                        values.Add(option);
                    }
                }

                HashSet<string> variableNames = [];
                foreach (StringEnumOption option in values)
                {
                    if (!variableNames.Add(option.VariableName))
                    {
                        sourceProductionContext.ReportDiagnostic(Diagnostic.Create(s_duplicateVariableName, classDecl.GetLocation(), option.VariableName));
                        hasError = true;
                        break;
                    }
                }

                if (hasError)
                {
                    continue;
                }

                string ns = GetNamespace(classDecl);
                string name = classDecl.Identifier.Text;

                string source = GenerateClassFromValues(ns, name, values);
                sourceProductionContext.AddSource(name + ".StringEnum.g.cs", source);
            }
        });
    }

    private static string GetNamespace(SyntaxNode? node)
    {
        while (node != null)
        {
            if (node is NamespaceDeclarationSyntax nds)
            {
                return nds.Name.ToString();
            }
            if (node is FileScopedNamespaceDeclarationSyntax fnds)
            {
                return fnds.Name.ToString();
            }
            node = node.Parent;
        }

        return string.Empty;
    }

    private static string GenerateClassFromValues(string ns, string name, List<StringEnumOption> values)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using TcfOss.DataStructures.Enums;");
        sb.AppendLine("using TcfOss.DataStructures.ValueCollections;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(ns))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"namespace {ns};");
            sb.AppendLine();
        }
        sb.AppendLine(CultureInfo.InvariantCulture, $"[GeneratedCode(\"TcfOss.StringEnumGenerator\", \"{s_toolVersion}\")]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"public sealed partial class {name} : IStringEnum<{name}>, IEquatable<{name}>");
        sb.AppendLine("{");
        sb.AppendLine("    private static ValueArray<string> s_allowedValues = new ValueArray<string>(new string[] {");
        for (int i = 0; i < values.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        \"{values[i].Text}\",");
            foreach (string other in values[i].OtherValues)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        \"{other}\",");
            }
        }
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    public static ValueArray<string> AllowedValues => s_allowedValues;");
        sb.AppendLine();
        sb.AppendLine("    private readonly string _text;");
        sb.AppendLine();
        sb.AppendLine("    public string Text => _text;");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"    private {name}(string text)");
        sb.AppendLine("    {");
        sb.AppendLine("        _text = text;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override string ToString()");
        sb.AppendLine("    {");
        sb.AppendLine("        return _text;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static " + name + " Parse(string text)");
        sb.AppendLine("    {");
        sb.AppendLine("        return text.ToUpperInvariant() switch");
        sb.AppendLine("        {");

        for (int i = 0; i < values.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"            \"{values[i].Text.ToUpperInvariant()}\" => {values[i].VariableName},");
            foreach (string other in values[i].OtherValues)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"            \"{other.ToUpperInvariant()}\" => {values[i].VariableName},");
            }
        }

        sb.AppendLine("            _ => throw new ArgumentException($\"Unknown " + name + ": {text}\")");
        sb.AppendLine("        }; ");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static bool TryParse(string text, [NotNullWhen(true)] out " + name + "? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            value = Parse(text);");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (ArgumentException)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private bool Equals(" + name + "? other) => ReferenceEquals(this, other);");
        sb.AppendLine();
        sb.AppendLine("    public override bool Equals(object? obj) => ReferenceEquals(this, obj);");
        sb.AppendLine();
        sb.AppendLine("    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);");
        sb.AppendLine();
        sb.AppendLine("    bool IEquatable<" + name + ">.Equals(" + name + "? other) => Equals(other);");
        sb.AppendLine();
        sb.AppendLine("    public static bool operator ==(" + name + "? left, " + name + "? right) => ReferenceEquals(left, right);");
        sb.AppendLine("    public static bool operator !=(" + name + "? left, " + name + "? right) => !ReferenceEquals(left, right);");
        sb.AppendLine();

        for (int i = 0; i < values.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"    public static readonly {name} {values[i].VariableName} = new(\"{values[i].Text}\");");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static StringEnumOption ToStringEnumOption(List<string> values)
    {
        if (values.Count == 0)
        {
            return new StringEnumOption("", "") { Error = s_noValue };
        }

        StringEnumOption result;
        if (values.Count == 1)
        {
            string variableName = values[0].ToVariableName();
            IdentifierError error = variableName.IsValidCSharpIdentifier();
            if (error != IdentifierError.None)
            {
                return new StringEnumOption(values[0], variableName)
                {
                    Error = s_cannotConstructVariableName,
                    ErrorArgs = new ValueArray<string>([values[0]])
                };
            }
            result = new StringEnumOption(values[0], variableName);
        }
        else if (values.Count == 2)
        {
            result = new StringEnumOption(values[0], values[1]);
        }
        else
        {
            result = new StringEnumOption(values[0], values[1], new ValueArray<string>([.. values[2..]]));
        }

        if (string.IsNullOrWhiteSpace(result.Text))
        {
            return result with { Error = s_emptyText };
        }

        IdentifierError idError = result.VariableName.IsValidCSharpIdentifier();
        if (idError == IdentifierError.Empty)
        {
            return result with { Error = s_emptyVariableName };
        }
        if (idError == IdentifierError.Invalid)
        {
            return result with { Error = s_invalidVariableName, ErrorArgs = new ValueArray<string>([result.VariableName]) };
        }
        else if (idError == IdentifierError.ReservedKeyword)
        {
            return result with { Error = s_variableNameReserved, ErrorArgs = new ValueArray<string>([result.VariableName]) };
        }
        else if (idError == IdentifierError.UsedVariableName)
        {
            return result with { Error = s_variableNameAlreadyUsed, ErrorArgs = new ValueArray<string>([result.VariableName]) };
        }

        return result;
    }

    private static readonly string s_toolVersion =
        typeof(StringEnumGenerator).Assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version ?? "0.0.0.0";

    private static readonly DiagnosticDescriptor s_noValue = new(
        id: "SEG001",
        title: "StringEnum generation error: No arguments",
        messageFormat: "StringEnum attribute must have at least one argument",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_emptyText = new(
        id: "SEG002",
        title: "StringEnum generation error: Empty text value",
        messageFormat: "Text value cannot be null or whitespace",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_emptyVariableName = new(
        id: "SEG003",
        title: "StringEnum generation error: Empty variable name",
        messageFormat: "VariableName cannot be null or whitespace",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_cannotConstructVariableName = new(
        id: "SEG004",
        title: "StringEnum generation error: Cannot construct variable name",
        messageFormat: "Cannot construct a valid C# identifier from text value '{0}'",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_invalidVariableName = new(
        id: "SEG005",
        title: "StringEnum generation error: Invalid variable name",
        messageFormat: "VariableName '{0}' is not a valid C# identifier",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_duplicateVariableName = new(
        id: "SEG006",
        title: "StringEnum generation error: Duplicate variable name",
        messageFormat: "Duplicate VariableName '{0}' in StringEnum attributes",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_variableNameReserved = new(
        id: "SEG007",
        title: "StringEnum generation error: Invalid variable name",
        messageFormat: "VariableName '{0}' is a C# reserved keyword",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor s_variableNameAlreadyUsed = new(
        id: "SEG008",
        title: "StringEnum generation error: Invalid variable name",
        messageFormat: "VariableName '{0}' is already used by StringEnum-generated code",
        category: "Syntax",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
