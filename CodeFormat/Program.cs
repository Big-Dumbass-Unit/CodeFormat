using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NDesk.Options;

namespace BDU.Tools.CodeFormat;

public static class Program
{
    private static readonly string[] unityMessages = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "UnityMessages.txt"));

    private static readonly string[] rankNames =
    {
        "event / event field", "delegate", "readonly field / property", "public field / property",
        "protected field / property", "private field / property", "other field / property",
        "constructor", "Unity message", "public method", "protected method", "private method",
        "other method", "nested type"
    };

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

                SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file);
                SyntaxNode root = tree.GetRoot();

                foreach (TypeDeclarationSyntax type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    int highestSoFar = 0;
                    string previousName = "";

                    foreach (MemberDeclarationSyntax member in type.Members)
                    {
                        int rank = Rank(member);

                        if (rank < highestSoFar)
                        {
                            Report(file, member, $"{type.Identifier}: '{Name(member)}' ({rankNames[rank]}) should come before '{previousName}' ({rankNames[highestSoFar]})");
                        }
                        else
                        {
                            highestSoFar = rank;
                            previousName = Name(member);
                        }

                        CheckMemberNaming(file, type, member);
                    }
                }

                foreach (BaseTypeDeclarationSyntax type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    string typeName = type.Identifier.Text;

                    if (type is InterfaceDeclarationSyntax)
                    {
                        if (typeName.Length < 2 || typeName[0] != 'I' || !char.IsUpper(typeName[1]))
                        {
                            Report(file, type, $"interface '{typeName}' should be named 'I' + PascalCase");
                        }
                    }
                    else if (!IsPascalCase(typeName))
                    {
                        Report(file, type, $"type '{typeName}' should be PascalCase");
                    }

                    if (type is EnumDeclarationSyntax enumType)
                    {
                        foreach (EnumMemberDeclarationSyntax enumMember in enumType.Members)
                        {
                            if (!IsPascalCase(enumMember.Identifier.Text))
                            {
                                Report(file, type, $"{typeName}: enum member '{enumMember.Identifier.Text}' should be PascalCase");
                            }
                        }
                    }
                }

                foreach (ParameterSyntax parameter in root.DescendantNodes().OfType<ParameterSyntax>())
                {
                    string paramName = parameter.Identifier.Text;

                    if (paramName.Length == 0 || paramName == "_")
                    {
                        continue;
                    }

                    if (!IsCamelCase(paramName))
                    {
                        Report(file, parameter, $"parameter '{paramName}' should be camelCase");
                    }
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

    private static void Report(string file, SyntaxNode node, string message)
    {
        int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        string display = file.Replace('\\', '/');

        if (display.StartsWith("./"))
        {
            display = display[2..];
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(onCI
        ? $"::error file={file.Replace('\\', '/')},line={line}::{message}"
        : $"{file}({line}): {message}");
        Console.ForegroundColor = originalColor;

        violations++;
    }

    private static int Rank(MemberDeclarationSyntax member)
    {
        return member switch
        {
            EventDeclarationSyntax or EventFieldDeclarationSyntax => 0,
            DelegateDeclarationSyntax => 1,
            FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) => 2,
            FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.PublicKeyword) => 3,
            FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.ProtectedKeyword) => 4,
            FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.PrivateKeyword) => 5,
            FieldDeclarationSyntax or PropertyDeclarationSyntax => 6,
            ConstructorDeclarationSyntax => 7,
            MethodDeclarationSyntax m when IsUnityMessage(m.Identifier.Text) => 8,
            MethodDeclarationSyntax m when m.Modifiers.Any(SyntaxKind.PublicKeyword) => 9,
            MethodDeclarationSyntax m when m.Modifiers.Any(SyntaxKind.ProtectedKeyword) => 10,
            MethodDeclarationSyntax m when m.Modifiers.Any(SyntaxKind.PrivateKeyword) => 11,
            MethodDeclarationSyntax or OperatorDeclarationSyntax => 12,
            BaseTypeDeclarationSyntax => 13,
            _ => 0
        };
    }

    private static bool IsUnityMessage(string name)
    {
        return unityMessages.Contains(name);
    }

    private static string Name(MemberDeclarationSyntax member)
    {
        return member switch
        {
            FieldDeclarationSyntax f => f.Declaration.Variables.First().Identifier.Text,
            EventFieldDeclarationSyntax e => e.Declaration.Variables.First().Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            BaseTypeDeclarationSyntax t => t.Identifier.Text,
            _ => member.Kind().ToString()
        };
    }

    private static void CheckMemberNaming(string file, TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case FieldDeclarationSyntax f:
                bool isConst = f.Modifiers.Any(SyntaxKind.ConstKeyword);

                foreach (VariableDeclaratorSyntax variable in f.Declaration.Variables)
                {
                    string name = variable.Identifier.Text;

                    if (isConst && !IsPascalCase(name))
                    {
                        Report(file, variable, $"{type.Identifier}: constant '{name}' should be PascalCase");
                    }
                    else if (!isConst && !IsCamelCase(name))
                    {
                        Report(file, variable, $"{type.Identifier}: field '{name}' should be camelCase");
                    }
                }
                break;

            case EventFieldDeclarationSyntax e:
                foreach (VariableDeclaratorSyntax variable in e.Declaration.Variables)
                {
                    if (!IsCamelCase(variable.Identifier.Text))
                    {
                        Report(file, variable, $"{type.Identifier}: event '{variable.Identifier.Text}' should be camelCase");
                    }
                }
                break;

            case PropertyDeclarationSyntax p when !IsCamelCase(p.Identifier.Text):
                Report(file, p, $"{type.Identifier}: property '{p.Identifier.Text}' should be camelCase");
                break;

            case MethodDeclarationSyntax m when !IsUnityMessage(m.Identifier.Text) && !IsPascalCase(m.Identifier.Text):
                Report(file, m, $"{type.Identifier}: method '{m.Identifier.Text}' should be PascalCase");
                break;
        }
    }

    private static bool IsPascalCase(string name) => char.IsUpper(name[0]) && !name.Contains('_');
    private static bool IsCamelCase(string name) => char.IsLower(name[0]) && !name.Contains('_');
}