using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace NHibernateAnalyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PropertyMustBeVirtualCodeFix)), Shared]
    public class PropertyMustBeVirtualCodeFix : CodeFixProvider
    {
        private const string title = "Make virtual";

        // Tells Roslyn which diagnostics are fixed by this provider
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(PropertyMustBeVirtualAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        // Registers code actions that will fix diagnostics
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // Start with gettings syntax root
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Get diagnostic that will be fixed...
            var diagnostic = context.Diagnostics.First();

            // ... and its location
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // When we've got location we can get token and syntax node;
            var node = root.FindToken(context.Span.Start).Parent;
            var property = node as PropertyDeclarationSyntax;

            // Create new property with additional 'virtual' modifier
            var virtualProperty = property.AddModifiers(SyntaxFactory.Token(SyntaxKind.VirtualKeyword));

            // Replace non-virtual property with virtual one in syntax root 
            var newRoot = root.ReplaceNode(property, virtualProperty);                       

            // Register code fix action
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => Task.FromResult(context.Document.WithSyntaxRoot(newRoot)),
                    equivalenceKey: title),
                diagnostic);
        }
    }
}