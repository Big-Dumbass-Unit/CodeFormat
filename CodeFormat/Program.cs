using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using NDesk.Options;

namespace BDU.Tools.CodeFormat;

public static class Program
{
    private static readonly string[] unityMessages = File.ReadAllLines("UnityMessages.txt");

    private static readonly string[] rankNames =
    {
        "event / event field", "delegate", "readonly field / property", "public field / property", 
        "protected field / property", "private field / property", "other field / property",
        "constructor", "Unity message", "public method", "protected method", "private method", 
        "other method", "nested type"  
    };

    private static int violations = 0;

    private static bool onCI = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    private static string root = string.Empty;
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

        foreach (string path in pathsToCheck)
        {
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

                        // CheckMemberNaming(file, type, member);
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

    // private static void CheckMemberNaming(string file, TypeDeclarationSyntax type, MemberDeclarationSyntax member)
    // {
    //     switch (member)
    //     {
    //         case FieldDeclarationSyntax f:
    //             bool isConst = f.Modifiers.Any(SyntaxKind.ConstKeyword);

    //             foreach (VariableDeclaratorSyntax variable in f.Declaration.Variables)
    //             {
    //                 string name = variable.Identifier.Text;

    //                 if (isConst && !IsPascalCase(name))
    //                 {
                        
    //                 }
    //             }
    //     }
    // }
}