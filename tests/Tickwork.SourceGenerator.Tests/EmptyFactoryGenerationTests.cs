using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tickwork.SourceGenerator;

namespace Tickwork.SourceGenerator.Tests;

/// <summary>
/// Tests that the source generator does NOT emit TickworkInstanceFactory.g.cs
/// when no [TickerFunction]-annotated methods are found in the assembly.
/// </summary>
public class EmptyFactoryGenerationTests
{
    private const string StubTypes = @"
namespace Tickwork.Utilities.Base
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class TickerFunctionAttribute : System.Attribute
    {
        public TickerFunctionAttribute(string functionName, string cronExpression = null, int taskPriority = 0, int maxConcurrency = 0) { }
    }

    public class TickerFunctionContext { }
    public class TickerFunctionContext<T> : TickerFunctionContext { public T Request { get; } }
}

namespace Tickwork.Utilities.Interfaces
{
    public interface ITickerFunctionBase { }

    public interface ITickerFunction : ITickerFunctionBase
    {
        System.Threading.Tasks.Task ExecuteAsync(
            Tickwork.Utilities.Base.TickerFunctionContext context,
            System.Threading.CancellationToken cancellationToken = default);
    }

    public interface ITickerFunction<TRequest> : ITickerFunctionBase
    {
        System.Threading.Tasks.Task ExecuteAsync(
            Tickwork.Utilities.Base.TickerFunctionContext<TRequest> context,
            System.Threading.CancellationToken cancellationToken = default);
    }
}

namespace Tickwork.Utilities
{
    public delegate System.Threading.Tasks.Task TickerFunctionDelegate(
        System.Threading.CancellationToken cancellationToken,
        System.IServiceProvider serviceProvider,
        Tickwork.Utilities.Base.TickerFunctionContext context);

    public static class TickerFunctionProvider
    {
        public static void RegisterFunctions(
            System.Collections.Generic.Dictionary<string, (string, Tickwork.Utilities.Enums.TickerTaskPriority, TickerFunctionDelegate, int)> f, int c) { }
        public static void RegisterRequestType(
            System.Collections.Generic.Dictionary<string, (string, System.Type)> t, int c) { }
    }
}

namespace Tickwork.Utilities.Enums
{
    public enum TickerTaskPriority { Normal = 0 }
}
";

    #region Interface-only — no [TickerFunction] attribute

    [Fact]
    public void InterfaceOnly_WithoutAttribute_DoesNotGenerateFactory()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Base;
using Tickwork.Utilities.Interfaces;

namespace TestApp
{
    public class InterfaceOnlyJob : ITickerFunction
    {
        public Task ExecuteAsync(TickerFunctionContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}";

        var generated = GetGeneratedFactorySource(source);

        // No [TickerFunction] attribute present → factory file must not be emitted
        Assert.Empty(generated);
    }

    [Fact]
    public void GenericInterfaceOnly_WithoutAttribute_DoesNotGenerateFactory()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Tickwork.Utilities.Base;
using Tickwork.Utilities.Interfaces;

namespace TestApp
{
    public class EmailPayload { public string To { get; set; } }

    public class EmailJob : ITickerFunction<EmailPayload>
    {
        public Task ExecuteAsync(TickerFunctionContext<EmailPayload> context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}";

        var generated = GetGeneratedFactorySource(source);

        Assert.Empty(generated);
    }

    [Fact]
    public void EmptyClass_NoMethodsAtAll_DoesNotGenerateFactory()
    {
        var source = @"
namespace TestApp
{
    public class EmptyClass { }
}";

        var generated = GetGeneratedFactorySource(source);

        Assert.Empty(generated);
    }

    #endregion

    #region Helpers

    private string GetGeneratedFactorySource(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new TickworkIncrementalSourceGenerator();
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions);
        driver = driver.RunGenerators(compilation);

        var results = driver.GetRunResult();
        var generatedSource = results.Results
            .SelectMany(r => r.GeneratedSources)
            .FirstOrDefault(s => s.HintName == "TickworkInstanceFactory.g.cs");

        return generatedSource.SourceText?.ToString() ?? string.Empty;
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);
        var stubTree = CSharpSyntaxTree.ParseText(StubTypes, parseOptions);

        var references = new List<MetadataReference>();

        var trustedPaths = ((string?)AppDomain.CurrentDomain.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator) ?? Array.Empty<string>();

        foreach (var path in trustedPaths)
        {
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        return CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree, stubTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
