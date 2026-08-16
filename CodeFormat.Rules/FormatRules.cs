#pragma warning disable RS2008

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BDU.Tools.CodeFormat.Rules
{
    public static class FormatRules
    {
        private const string ResourceName = "CodeFormat.Rules.UnityMessages.txt";

        public static readonly DiagnosticDescriptor memberOrder = Rule("BDU0001", "Member ordering");
        public static readonly DiagnosticDescriptor fieldNaming = Rule("BDU0002", "Field naming");
        public static readonly DiagnosticDescriptor constantNaming = Rule("BDU0003", "Constant naming");
        public static readonly DiagnosticDescriptor eventNaming = Rule("BDU0004", "Event naming");
        public static readonly DiagnosticDescriptor propertyNaming = Rule("BDU0005", "Constant naming");
        public static readonly DiagnosticDescriptor methodNaming = Rule("BDU0006", "Method naming");
        public static readonly DiagnosticDescriptor typeNaming = Rule("BDU0007", "Type naming");
        public static readonly DiagnosticDescriptor interfaceNaming = Rule("BDU0008", "Interface naming");
        public static readonly DiagnosticDescriptor enumMemberNaming = Rule("BDU0009", "Enum member naming");
        public static readonly DiagnosticDescriptor parameterNaming = Rule("BDU0010", "Parameter naming");
        public static readonly DiagnosticDescriptor variableType = Rule("BDU0011", "Variable type");

        private static readonly HashSet<string> unityMessages = LoadUnityMessages();

        private static readonly string[] rankNames =
        {
            "event / event field", "delegate", "constant field / property", "readonly field / property", 
            "public field / property", "protected field / property", "private field / property", 
            "other field / property", "constructor", "Unity message", "public method", "protected method", 
            "private method", "other method", "nested type"
        };

        public static IEnumerable<Diagnostic> AnalyzeType(TypeDeclarationSyntax type)
        {
            int highestSoFar = 0;
            string previousName = "";

            foreach (MemberDeclarationSyntax member in type.Members)
            {
                int rank = Rank(member);

                if (rank >= 0)
                {
                    if (rank < highestSoFar)
                    {
                        yield return Diagnostic.Create(memberOrder, member.GetLocation(), $"{type.Identifier}: '{Name(member)}' ({rankNames[rank]}) should come before '{previousName}' ({rankNames[highestSoFar]})");
                    }
                    else
                    {
                        highestSoFar = rank;
                        previousName = Name(member);
                    }
                }

                foreach (Diagnostic diagnostic in CheckMemberNaming(type, member))
                {
                    yield return diagnostic;
                }
            }
        }

        public static IEnumerable<Diagnostic> AnalyzeBaseType(BaseTypeDeclarationSyntax type)
        {
            string typeName = type.Identifier.Text;

            if (type is InterfaceDeclarationSyntax)
            {
                if (typeName.Length > 0 && (typeName.Length < 2 || typeName[0] != 'I' || !char.IsUpper(typeName[1])))
                {
                    yield return Diagnostic.Create(interfaceNaming, type.Identifier.GetLocation(), $"interface '{typeName}' should be named 'I' + PascalCase");
                }
            }
            else if (!IsPascalCase(typeName))
            {
                yield return Diagnostic.Create(typeNaming, type.Identifier.GetLocation(), $"type '{typeName}' should be PascalCase");
            }

            if (type is EnumDeclarationSyntax enumType)
            {
                foreach (EnumMemberDeclarationSyntax enumMember in enumType.Members)
                {
                    if (!IsPascalCase(enumMember.Identifier.Text))
                    {
                        yield return Diagnostic.Create(enumMemberNaming, enumMember.Identifier.GetLocation(), $"{typeName}: enum member '{enumMember.Identifier.Text}' should be PascalCase");
                    }
                }
            }
        }

        public static IEnumerable<Diagnostic> AnalyzeParameter(ParameterSyntax parameter)
        {
            string parameterName = parameter.Identifier.Text;

            if (parameterName.Length == 0 || parameterName == "_")
            {
                yield break;
            }

            if (!IsCamelCase(parameterName))
            {
                yield return Diagnostic.Create(parameterNaming, parameter.Identifier.GetLocation(), $"parameter '{parameterName}' should be camelCase");
            }
        }

        public static IEnumerable<Diagnostic> AnalyzeVariableType(SyntaxNode node)
        {
            if (node is VarPatternSyntax pattern)
            {
                yield return Diagnostic.Create(variableType, pattern.VarKeyword.GetLocation(), "'var' should be replaced with an explicit type");
                yield break;
            }

            TypeSyntax type = node switch
            {
                VariableDeclarationSyntax declaration => declaration.Type,
                ForEachStatementSyntax forEach => forEach.Type,
                DeclarationExpressionSyntax declaration => declaration.Type,
                _ => null
            };

            if (type is RefTypeSyntax reference)
            {
                type = reference.Type;
            }

            if (type != null && type.IsVar)
            {
                yield return Diagnostic.Create(variableType, type.GetLocation(), "'var' should be replaced with an explicit type");
            }
        }

        private static IEnumerable<Diagnostic> CheckMemberNaming(TypeDeclarationSyntax type, MemberDeclarationSyntax member)
        {
            if (member.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                yield break;
            }

            switch (member)
            {
                case FieldDeclarationSyntax field:
                    bool isConst = field.Modifiers.Any(SyntaxKind.ConstKeyword);

                    foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    {
                        string name = variable.Identifier.Text;

                        if (isConst && !IsPascalCase(name))
                        {
                            yield return Diagnostic.Create(constantNaming, variable.Identifier.GetLocation(), $"{type.Identifier}: constant '{name}' should be PascalCase");
                        }
                        else if (!isConst && !IsCamelCase(name))
                        {
                            yield return Diagnostic.Create(fieldNaming, variable.Identifier.GetLocation(), $"{type.Identifier}: field '{name}' should be camelCase");
                        }
                    }
                    break;

                case EventFieldDeclarationSyntax eventField:
                    foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
                    {
                        if (!IsCamelCase(variable.Identifier.Text))
                        {
                            yield return Diagnostic.Create(eventNaming, variable.Identifier.GetLocation(), $"{type.Identifier}: event '{variable.Identifier.Text}' should be camelCase");
                        }
                    }
                    break;

                case PropertyDeclarationSyntax property when !IsCamelCase(property.Identifier.Text):
                    yield return Diagnostic.Create(propertyNaming, property.Identifier.GetLocation(), $"{type.Identifier}: property '{property.Identifier.Text}' should be camelCase");
                    break;

                case MethodDeclarationSyntax method when !IsUnityMessage(method.Identifier.Text) && !IsPascalCase(method.Identifier.Text):
                    yield return Diagnostic.Create(methodNaming, method.Identifier.GetLocation(), $"{type.Identifier}: method '{method.Identifier.Text}' should be PascalCase");
                    break;
            }
        }

        private static int Rank(MemberDeclarationSyntax member)
        {
            return member switch
            {
                EventDeclarationSyntax or EventFieldDeclarationSyntax => 0,
                DelegateDeclarationSyntax => 1,
                FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.ConstKeyword) => 2,
                FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) => 3,
                FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.PublicKeyword) => 4,
                FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.ProtectedKeyword) => 5,
                FieldDeclarationSyntax or PropertyDeclarationSyntax when member.Modifiers.Any(SyntaxKind.PrivateKeyword) => 6,
                FieldDeclarationSyntax or PropertyDeclarationSyntax => 7,
                ConstructorDeclarationSyntax => 8,
                MethodDeclarationSyntax method when IsUnityMessage(method.Identifier.Text) => 9,
                MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.PublicKeyword) => 10,
                MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.ProtectedKeyword) => 11,
                MethodDeclarationSyntax method when method.Modifiers.Any(SyntaxKind.PrivateKeyword) => 12,
                MethodDeclarationSyntax or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => 13,
                BaseTypeDeclarationSyntax => 14,
                _ => -1
            };
        }

        private static string Name(MemberDeclarationSyntax member)
        {
            return member switch
            {
                FieldDeclarationSyntax field => VariableName(field.Declaration),
                EventFieldDeclarationSyntax eventField => VariableName(eventField.Declaration),
                PropertyDeclarationSyntax property => property.Identifier.Text,
                MethodDeclarationSyntax method => method.Identifier.Text,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.Text,
                BaseTypeDeclarationSyntax type => type.Identifier.Text,
                _ => member.Kind().ToString()
            };
        }

        private static string VariableName(VariableDeclarationSyntax declaration)
        {
            VariableDeclaratorSyntax first = declaration.Variables.FirstOrDefault();
            return first == null ? "" : first.Identifier.Text;
        }

        private static bool IsUnityMessage(string name)
        {
            return unityMessages.Contains(name);
        }

        private static HashSet<string> LoadUnityMessages()
        {
            using (Stream stream = typeof(FormatRules).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found. Available: {string.Join(", ", typeof(FormatRules).Assembly.GetManifestResourceNames())}");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return new HashSet<string>(reader.ReadToEnd()
                        .Split('\n')
                        .Select(line => line.Trim())
                        .Where(line => line.Length > 0));
                }
            }
        }

        private static DiagnosticDescriptor Rule(string id, string title)
        {
            return new DiagnosticDescriptor(id, title, "{0}", "CodeFormat", DiagnosticSeverity.Error, true);
        }

        private static bool IsPascalCase(string name) => name.Length == 0 || (char.IsUpper(name[0]) && !name.Contains("_"));
        private static bool IsCamelCase(string name) => name.Length == 0 || (char.IsLower(name[0]) && !name.Contains("_"));
    }
}
