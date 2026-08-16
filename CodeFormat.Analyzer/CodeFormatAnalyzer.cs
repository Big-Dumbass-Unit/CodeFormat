using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using BDU.Tools.CodeFormat.Rules;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace BDU.Tools.CodeFormat.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeFormatAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            FormatRules.memberOrder,
            FormatRules.fieldNaming,
            FormatRules.constantNaming,
            FormatRules.eventNaming,
            FormatRules.propertyNaming,
            FormatRules.methodNaming,
            FormatRules.typeNaming,
            FormatRules.interfaceNaming,
            FormatRules.enumMemberNaming,
            FormatRules.parameterNaming,
            FormatRules.variableType
        ];

        private static readonly string[] ignoredFolders = { "Plugins", "ThirdParty" };

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);

            context.RegisterSyntaxNodeAction(AnalyzeBaseTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration,
                SyntaxKind.EnumDeclaration);

            context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);

            context.RegisterSyntaxNodeAction(AnalyzeVariableType,
                SyntaxKind.VariableDeclaration,
                SyntaxKind.ForEachStatement,
                SyntaxKind.DeclarationExpression,
                SyntaxKind.VarPattern);
        }

        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (IsIgnored(context.Node.SyntaxTree))
            {
                return;
            }

            Report(context, FormatRules.AnalyzeType((TypeDeclarationSyntax)context.Node));
        }

        private static void AnalyzeBaseTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (IsIgnored(context.Node.SyntaxTree))
            {
                return;
            }

            Report(context, FormatRules.AnalyzeBaseType((BaseTypeDeclarationSyntax)context.Node));
        }

        private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
        {
            if (IsIgnored(context.Node.SyntaxTree))
            {
                return;
            }

            Report(context, FormatRules.AnalyzeParameter((ParameterSyntax)context.Node));
        }

        private static void AnalyzeVariableType(SyntaxNodeAnalysisContext context)
        {
            if (IsIgnored(context.Node.SyntaxTree))
            {
                return;
            }

            Report(context, FormatRules.AnalyzeVariableType(context.Node));
        }

        private static void Report(SyntaxNodeAnalysisContext context, IEnumerable<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsIgnored(SyntaxTree tree)
        {
            string path = tree.FilePath;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string[] segments = path.Replace('\\', '/').Split('/');
            return !segments.Contains("Assets") || segments.Any(ignoredFolders.Contains);
        }
    }
}