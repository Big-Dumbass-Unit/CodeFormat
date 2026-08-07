using BDU.Tools.CodeFormat.Rules;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NDesk.Options;

namespace BDU.Tools.CodeFormat.CLI;

public static class Program
{
    private static int violations = 0;

    private static bool onCI = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    private static ConsoleColor originalColor = Console.ForegroundColor;

    public static int Main(string[] args)
    {
        bool help = false;

        List<string> ignoredPaths = new List<string>();
        List<string> pathsToCheck = new List<string>();
        List<string> current = pathsToCheck;

        OptionSet options = new OptionSet()
        {
            { "i=|ignore=", "Specifies the folder(s) to ignore when checking the formats.", v => { ignoredPaths.Add(v); current = ignoredPaths; } },
            { "h|help|?", "Shows this message.", v => help = v != null },
            { "p=|path=", "Specifies the path(s) to format check.", v => { pathsToCheck.Add(v); current = pathsToCheck; } },
            { "<>", v => current.Add(v) }
        };
        options.Parse(args);

        if (help)
        {
            Console.WriteLine("Usage: CodeFormat [OPTIONS] + path(s) to check");
            Console.WriteLine("Checks the formatting of a specified folder (and subdirectories) alongside options.\n");
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);
            return 0;
        }

        if (pathsToCheck.Count == 0)
        {
            pathsToCheck.Add(".");
        }

        foreach (string path in pathsToCheck)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine(onCI
                    ? $"::error::Path '{path}' does not exist."
                    : $"error: path '{path}' does not exist.");
                return 2;
            }

            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(path, file);

                if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(ignoredPaths.Contains))
                {
                    continue;
                }

                SyntaxNode root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot();

                IEnumerable<Diagnostic> diagnostics = root.DescendantNodes().OfType<TypeDeclarationSyntax>().SelectMany(FormatRules.AnalyzeType)
                    .Concat(root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().SelectMany(FormatRules.AnalyzeBaseType))
                    .Concat(root.DescendantNodes().OfType<ParameterSyntax>().SelectMany(FormatRules.AnalyzeParameter));

                foreach (Diagnostic diagnostic in diagnostics)
                {
                    Print(file, diagnostic);
                }
            }
        }

        Console.ForegroundColor = violations == 0 ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(violations == 0
            ? "CodeFormat successfully found no violations."
            : $"CodeFormat found {violations} violation(s).");
        Console.ForegroundColor = originalColor;

        return violations == 0 ? 0 : 1;
    }

    private static void Print(string file, Diagnostic diagnostic)
    {
        int line = diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1;
        string display = file.Replace('\\', '/');

        if (display.StartsWith("./"))
        {
            display = display[2..];
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(onCI
            ? $"::error file={display},line={line}::{diagnostic.GetMessage()}"
            : $"{display}({line}): {diagnostic.GetMessage()}");
        Console.ForegroundColor = originalColor;

        violations++;
    }
}