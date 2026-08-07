using System.Collections.Generic;
using System.Collections.Immutable;

using BDU.Tools.CodeFormat.Rules;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CodeFormat.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CodeFormatAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(FormatRules.memberOrder, FormatRules.fieldNaming, FormatRules.constantNaming, FormatRules.eventNaming,
            FormatRules.propertyNaming, FormatRules.methodNaming, FormatRules.typeNaming, FormatRules.interfaceNaming, FormatRules.enumMemberNaming, FormatRules.parameterNaming);

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
        }

        private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            Report(context, FormatRules.AnalyzeType((TypeDeclarationSyntax)context.Node));
        }

        private static void AnalyzeBaseTypeDeclaration(SyntaxNodeAnalysisContext context)
        {
            Report(context, FormatRules.AnalyzeBaseType((BaseTypeDeclarationSyntax)context.Node));
        }

        private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
        {
            Report(context, FormatRules.AnalyzeParameter((ParameterSyntax)context.Node));
        }

        private static void Report(SyntaxNodeAnalysisContext context, IEnumerable<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}