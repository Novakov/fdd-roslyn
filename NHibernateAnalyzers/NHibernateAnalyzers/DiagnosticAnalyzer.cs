using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NHibernateAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PropertyMustBeVirtualAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NHibernateAnalyzers";


        private static readonly LocalizableString Title = "Property must be virtual";
        private static readonly LocalizableString MessageFormat = "Property '{0}' must be virtual";
        private static readonly LocalizableString Description = "All properties in NHibernate-enabled classes must be virtual";

        private const string Category = "NHibernate";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // This diagnostic analyzer works on property declaration syntax node
            context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);            
        }

        private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
        {
            // Get analyzed node
            var property = (PropertyDeclarationSyntax)context.Node;

            // Get declared symbol
            var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);

            // Get class
            var classSymbol = propertySymbol.ContainingType;

            // Check if any ancestor (or self) has attribute named NHibernateAttribute contained in namespace Domain
            var isNHibernateClass = AncestorsAndSelf(classSymbol)
                    .SelectMany(x => x.GetAttributes())
                    .Any(x => x.AttributeClass.Name == "NHibernateAttribute" && x.AttributeClass.ContainingNamespace.Name == "Domain")
                    ;

            // If not, it is not NHibernate class
            if (!isNHibernateClass)
            {
                return;
            }

            // Property is virtual if it is: virtual, override or abstract
            var isVirtual = propertySymbol.IsVirtual || propertySymbol.IsOverride || propertySymbol.IsAbstract;

            // If virtual, it is ok
            if (isVirtual)
            {
                return;
            }

            // If not virtual, report diagnostic for property
            var diagnostic = Diagnostic.Create(Rule, property.GetLocation(), property.Identifier.Text);

            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Helper methods that yields all ancestor of given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private IEnumerable<INamedTypeSymbol> AncestorsAndSelf(INamedTypeSymbol type)
        {
            var current = type;

            while (current != null)
            {
                yield return current;

                current = current.BaseType; 
            }
        }
    }
}
