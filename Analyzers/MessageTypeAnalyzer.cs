using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ubicomp.Utils.NET.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MessageTypeAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UBI001";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            "Type argument must have [MessageType] attribute",
            "Type '{0}' used in SendAsync must be decorated with [MessageType]",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Types used with TransportComponent.SendAsync<T> must explicitly define a MessageType ID via attributes.");

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
            if (method.Name != "SendAsync") return;

            // Check if containing type is TransportComponent or IMulticastSocket equivalent (usually TransportComponent)
            // We can check strict type name or just trust usage of generic SendAsync with 1 type arg
            var containingType = method.ContainingType;
            if (containingType?.Name != "TransportComponent" && containingType?.Name != "ITransportComponent") return;

            // Check if it's the generic overload sending the object
            if (!method.IsGenericMethod) return;

            var typeArg = method.TypeArguments[0];

            // Check for [MessageType] attribute
            // We look for attribute by name "MessageTypeAttribute" or just "MessageType"
            bool hasAttribute = false;
            foreach (var attr in typeArg.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "MessageTypeAttribute" ||
                    attr.AttributeClass?.Name == "MessageType")
                {
                    hasAttribute = true;
                    break;
                }
            }

            if (!hasAttribute)
            {
                var diagnostic = Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), typeArg.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
