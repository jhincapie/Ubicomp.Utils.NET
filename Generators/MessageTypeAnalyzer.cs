using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ubicomp.Utils.NET.Generators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MessageTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UbicompNET001";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Type missing [MessageType] attribute",
            "Type '{0}' is used in SendAsync but does not have the [MessageType] attribute",
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Types sent via TransportComponent.SendAsync should have the [MessageType] attribute for auto-discovery.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        }

        private void AnalyzeInvocation(OperationAnalysisContext context)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;

            // Check if method is SendAsync
            if (method.Name == "SendAsync" &&
                method.ContainingType.Name == "TransportComponent" &&
                method.IsGenericMethod)
            {
                var typeArgument = method.TypeArguments[0];

                // Check for [MessageType] attribute
                bool hasAttribute = false;
                foreach (var attr in typeArgument.GetAttributes())
                {
                    if (attr.AttributeClass != null && attr.AttributeClass.Name == "MessageTypeAttribute")
                    {
                        hasAttribute = true;
                        break;
                    }
                }

                if (!hasAttribute)
                {
                    // Check if base types have it? The attribute is inherently inherited?
                    // Typically attributes are not inherited unless AttributeUsage says so.
                    // MessageTypeAttribute implementation in source (not visible here) likely allows inheritance or not.
                    // Assuming precise match for now.

                    var diagnostic = Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), typeArgument.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
