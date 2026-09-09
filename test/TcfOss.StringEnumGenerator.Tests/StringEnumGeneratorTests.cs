using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace TcfOss.StringEnumGenerator.Tests;

public class StringEnumGeneratorTests
{
    public const string AttributeSource = """
        using System;
        namespace TcfOss.StringEnumGenerator
        {
            [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
            public class StringEnumAttribute(string value, string? variableName = null, params string[]? equivalentValues) : Attribute
            {
                public string Value { get; } = value;
                public string VariableName { get; } = variableName ?? value.ToVariableName();
                public string[] EquivalentValues { get; } = equivalentValues ?? [];
            }
        }
        """;

    [Fact]
    public void GeneratesSourceFromAttribute_FileNamespace()
    {
        // Source that uses the attribute
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("OPTION1")]
            [StringEnum("OPTIONB", "OptionB")]
            [StringEnum("OPTION_C")]
            [StringEnum("OPTION D")]
            [StringEnum("4OPTION")]
            [StringEnum("<>", "NotEqual", "!=")]
            public sealed partial class MyOptionsTest { }
            """;

        string combined = GenerateSource(annotatedSource);
        string[] sourceLines = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(sourceLines, sl => sl.Contains("public sealed partial class MyOptionsTest"));
        Assert.Single(sourceLines, sl => sl.Contains("Option1 = new(\"OPTION1\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionB = new(\"OPTIONB\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionC = new(\"OPTION_C\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionD = new(\"OPTION D\")"));
        Assert.Single(sourceLines, sl => sl.Contains("_4option = new(\"4OPTION\")"));
        Assert.Single(sourceLines, sl => sl.Contains("NotEqual = new(\"<>\")"));

        Assert.Single(sourceLines, sl => sl.Contains("=> Option1,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionB,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionC,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionD,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> _4option,"));
        Assert.Equal(2, sourceLines.Count(sl => sl.Contains("=> NotEqual,")));
    }

    [Fact]
    public void GeneratesSourceFromAttribute_RegularNamespace()
    {
        // Source that uses the attribute
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes
            {
                [StringEnum("OPTION1")]
                [StringEnum("OPTIONB", "OptionB")]
                [StringEnum("OPTION_C")]
                [StringEnum("OPTION D")]
                [StringEnum("<>", "NotEqual", "!=")]
                public sealed partial class MyOptionsTest { }
            }
            """;

        string combined = GenerateSource(annotatedSource);
        string[] sourceLines = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(sourceLines, sl => sl.Contains("public sealed partial class MyOptionsTest"));
        Assert.Single(sourceLines, sl => sl.Contains("Option1 = new(\"OPTION1\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionB = new(\"OPTIONB\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionC = new(\"OPTION_C\")"));
        Assert.Single(sourceLines, sl => sl.Contains("OptionD = new(\"OPTION D\")"));
        Assert.Single(sourceLines, sl => sl.Contains("NotEqual = new(\"<>\")"));

        Assert.Single(sourceLines, sl => sl.Contains("=> Option1,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionB,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionC,"));
        Assert.Single(sourceLines, sl => sl.Contains("=> OptionD,"));
        Assert.Equal(2, sourceLines.Count(sl => sl.Contains("=> NotEqual,")));

        Assert.Single(sourceLines, sl => sl.Contains("operator =="));
        Assert.Single(sourceLines, sl => sl.Contains("operator !="));
    }

    [Fact]
    public void GeneratesSourceFromAttribute_GlobalNamespace()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            [StringEnum("OPTION1")]
            public sealed partial class MyOptionsTest { }
            """;

        string combined = GenerateSource(annotatedSource);
        string[] sourceLines = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(sourceLines, sl => sl.Contains("public sealed partial class MyOptionsTest"));
        Assert.Single(sourceLines, sl => sl.Contains("Option1 = new(\"OPTION1\")"));
    }

    [Fact]
    public void GeneratesSourceFromAttribute_HasCorrectVersion()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            [StringEnum("OPTION1")]
            public sealed partial class MyOptionsTest { }
            """;

        string combined = GenerateSource(annotatedSource);
        string[] sourceLines = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        string versionLine = sourceLines.Single(sl => sl.Contains("[GeneratedCode"));

#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
        Regex genAttrRegex = new(@"\[GeneratedCode\(""TcfOss\.StringEnumGenerator"", ""(?<version>[\d\.]+)""\)\]", RegexOptions.Compiled);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

        Match match = genAttrRegex.Match(versionLine);
        Assert.True(match.Success, "GeneratedCode attribute not found or in unexpected format");
        string version = match.Groups["version"].Value;
        Assert.Equal(typeof(StringEnumGenerator).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version, version);
    }

    [Fact]
    public void GenerateSourceFromAttribute_Reflective()
    {
        // Source that uses the attribute
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TestNamespace;

            [StringEnum("OPTION1")]
            [StringEnum("OPTIONB", "OptionB")]
            [StringEnum("OPTION_C")]
            [StringEnum("OPTION D")]
            [StringEnum("<>", "NotEqual", "!=", "NEQ")]
            public sealed partial class MyOptionsTest { }
            """;

        Type enumType = CompileGeneratedType(annotatedSource, "TestNamespace.MyOptionsTest");

        /* Validate that the expected static fields are generated */

        object? option1 = enumType.GetField("Option1")?.GetValue(null);
        object? optionB = enumType.GetField("OptionB")?.GetValue(null);
        object? optionC = enumType.GetField("OptionC")?.GetValue(null);
        object? optionD = enumType.GetField("OptionD")?.GetValue(null);
        object? notEqual = enumType.GetField("NotEqual")?.GetValue(null);

        Assert.NotNull(option1);
        Assert.NotNull(optionB);
        Assert.NotNull(optionC);
        Assert.NotNull(optionD);
        Assert.NotNull(notEqual);

        Assert.Equal("OPTION1", option1.ToString());
        Assert.Equal("OPTIONB", optionB.ToString());
        Assert.Equal("OPTION_C", optionC.ToString());
        Assert.Equal("OPTION D", optionD.ToString());
        Assert.Equal("<>", notEqual.ToString());

        /* Validate Parse */
        MethodInfo parseMethod = enumType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        object? parsedOption1 = parseMethod.Invoke(null, ["OPTION1"]);
        object? parsedOptionB = parseMethod.Invoke(null, ["OPTIONB"]);
        object? parsedOptionC = parseMethod.Invoke(null, ["OPTION_C"]);
        object? parsedOptionD = parseMethod.Invoke(null, ["OPTION D"]);
        object? parsedNotEqual1 = parseMethod.Invoke(null, ["<>"]);
        object? parsedNotEqual2 = parseMethod.Invoke(null, ["!="]);
        object? parsedNotEqual3 = parseMethod.Invoke(null, ["NEQ"]);

        Assert.Same(option1, parsedOption1);
        Assert.Same(optionB, parsedOptionB);
        Assert.Same(optionC, parsedOptionC);
        Assert.Same(optionD, parsedOptionD);
        Assert.Same(notEqual, parsedNotEqual1);
        Assert.Same(notEqual, parsedNotEqual2);
        Assert.Same(notEqual, parsedNotEqual3);

        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() => parseMethod.Invoke(null, ["INVALID"]));
        Assert.IsType<ArgumentException>(ex.InnerException);
        Assert.Equal("Unknown MyOptionsTest: INVALID", ex.InnerException?.Message);

        /* Validate TryParse */
        MethodInfo tryParseMethod = enumType.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static)!;
        object?[] tryParseArgsWorks = ["OPTION1", null];
        bool tryParseWorksSuccess = (bool)tryParseMethod.Invoke(null, tryParseArgsWorks)!;
        Assert.True(tryParseWorksSuccess);
        Assert.Same(option1, tryParseArgsWorks[1]);

        object?[] tryParseArgsFails = ["INVALID", null];
        bool tryParseFailsSuccess = (bool)tryParseMethod.Invoke(null, tryParseArgsFails)!;
        Assert.False(tryParseFailsSuccess);
        Assert.Null(tryParseArgsFails[1]);
    }

    [Fact]
    public void Generate_Error_NoArguments()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum()]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG001", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("StringEnum attribute must have at least one argument", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_EmptyText()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG002", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("Text value cannot be null or whitespace", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_EmptyVariableName()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG003", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName cannot be null or whitespace", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_CannotConstructVariableName()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("!!!")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG004", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("Cannot construct a valid C# identifier from text value '!!!'", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_InvalidVariableName_Blank()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG003", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName cannot be null or whitespace", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_InvalidVariableName_StartsWithNumber()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "123Invalid")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG005", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName '123Invalid' is not a valid C# identifier", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_InvalidVariableName_IsKeyword()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "class")]
            public sealed partial class MyOptionsTest { }
            """;
        _ = GenerateSource(annotatedSource);
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);
        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG007", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName 'class' is a C# reserved keyword", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_InvalidVariableName_IsContextualKeyword()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "equals")]
            public sealed partial class MyOptionsTest { }
            """;
        _ = GenerateSource(annotatedSource);
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);
        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG007", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName 'equals' is a C# reserved keyword", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_InvalidVariableName_IsUsedByGenerator()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST", "Equals")]
            public sealed partial class MyOptionsTest { }
            """;
        _ = GenerateSource(annotatedSource);
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);
        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG008", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("VariableName 'Equals' is already used by StringEnum-generated code", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Generate_Error_DuplicateVariableName()
    {
        string annotatedSource = """
            using TcfOss.StringEnumGenerator;
            namespace TcfOss.DatabaseManager.Core.Statements.Attributes;
            [StringEnum("TEST1", "DuplicateName")]
            [StringEnum("TEST2", "DuplicateName")]
            public sealed partial class MyOptionsTest { }
            """;

        _ = GenerateSource(annotatedSource);

        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(annotatedSource);

        Assert.Single(diagnostics);
        Diagnostic diag = diagnostics[0];
        Assert.Equal("SEG006", diag.Id);
        Assert.Equal(DiagnosticSeverity.Error, diag.Severity);
        Assert.Equal("Duplicate VariableName 'DuplicateName' in StringEnum attributes", diag.GetMessage(CultureInfo.InvariantCulture));
    }

    public static GeneratorDriverRunResult RunGenerator(string annotatedClassSource)
    {
        SyntaxTree[] syntaxTrees =
        [
            CSharpSyntaxTree.ParseText(annotatedClassSource, cancellationToken: TestContext.Current.CancellationToken),
            CSharpSyntaxTree.ParseText(AttributeSource, cancellationToken: TestContext.Current.CancellationToken),
        ];

        // Collect common references from the current test process
        var refs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Instantiate and run the generator
        var generator = new StringEnumGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        return runResult;
    }

    private static string GenerateSource(string annotatedClassSource)
    {
        GeneratorDriverRunResult runResult = RunGenerator(annotatedClassSource);
        GeneratorRunResult gen = runResult.Results.Single();

        string combined = string.Join("\n", gen.GeneratedSources.Select(gs => gs.SourceText.ToString()));
        return combined;
    }

    private static Type CompileGeneratedType(string annotatedSource, string fullTypeName)
    {
        string generatedSource = GenerateSource(annotatedSource);

        AssemblyLoadContext? loadContext = null;
        try
        {
            SyntaxTree[] allTrees =
            [
                CSharpSyntaxTree.ParseText(annotatedSource, cancellationToken: TestContext.Current.CancellationToken),
                CSharpSyntaxTree.ParseText(AttributeSource, cancellationToken: TestContext.Current.CancellationToken),
                CSharpSyntaxTree.ParseText(generatedSource, cancellationToken: TestContext.Current.CancellationToken),
            ];

            List<PortableExecutableReference> refs = [.. AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))];

            var compilation = CSharpCompilation.Create(
                assemblyName: "TestGenerated",
                syntaxTrees: allTrees,
                references: refs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult emitResult = compilation.Emit(ms, cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(emitResult.Success, "Compilation of generated code failed: " + string.Join("\n", emitResult.Diagnostics.Select(d => d.ToString())));

            ms.Seek(0, SeekOrigin.Begin);

            loadContext = new AssemblyLoadContext("test", isCollectible: true);
            loadContext.Resolving += (_, name) => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);

            Assembly assembly = loadContext.LoadFromStream(ms);
            Type enumType = assembly.GetType(fullTypeName)!;
            return enumType;
        }
        finally
        {
            loadContext?.Unload();
        }
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string annotatedClassSource)
    {
        GeneratorDriverRunResult runResult = RunGenerator(annotatedClassSource);
        GeneratorRunResult gen = runResult.Results.Single();

        ImmutableArray<Diagnostic> diagnostics = gen.Diagnostics;
        return diagnostics;
    }
}
