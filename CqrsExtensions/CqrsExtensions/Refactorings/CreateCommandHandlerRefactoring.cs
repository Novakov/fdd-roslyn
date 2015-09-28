using System;
using System.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Formatting;

namespace CqrsExtensions.Refactorings
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(CreateCommandHandlerRefactoring))]
    [Shared]
    public class CreateCommandHandlerRefactoring : CodeRefactoringProvider
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var tree = await document.GetSyntaxTreeAsync();
            var root = tree.GetRoot();

            var token = root.FindToken(context.Span.Start);

            // Get selected syntax node
            var node = token.Parent;

            // This refactoring works on class declaration
            var classDeclaration = node as ClassDeclarationSyntax;

            if (classDeclaration == null)
            {
                return;
            }

            // Semantic model gives us some information beyond syntax
            var semanticModel = await document.GetSemanticModelAsync();

            // We can get class symbol declared in node. 
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            var commandType = semanticModel.Compilation.CommandBaseTypeSymbol();

            // This works only on classes that derives from command type (symbol)
            if (classSymbol.BaseType != commandType)
            {
                return;
            }

            // Now we register code action that will add command handler for given command class
            var action = CodeAction.Create(
                    "Create command handler",
                    ct => CreateCommandHandler(context, classSymbol, ct)
                    );
           
            context.RegisterRefactoring(action);
        }

        private async Task<Solution> CreateCommandHandler(CodeRefactoringContext context, INamedTypeSymbol commandType, CancellationToken ct)
        {
            var commandHandlersProject = context.Document.Project.Solution.Projects.Single(x => x.Name == "CommandHandlers");

            // We construct type symbol representing base command handler class: CommandHandler<TCommand>. 
            // Construct makes closed-generic type from open-generic type
            var commandHandlerBaseType = commandHandlersProject.CommandHandlerBaseTypeSymbol().Construct(commandType);
            
            var newDocumentName = $"{commandType.Name}Handler.cs";

            // Syntax generator helps with generating type references
            var generator = SyntaxGenerator.GetGenerator(context.Document);
            
            // Simplifier annotation marks nodes for simplification. In this case reducing namespace-qualified name into simple name
            var baseType = (TypeSyntax)generator.TypeExpression(commandHandlerBaseType)
                .WithAdditionalAnnotations(Simplifier.Annotation);
            
            var commandTypeSyntax = (TypeSyntax)generator.TypeExpression(commandType)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            // New document is created bottom-up. 

            // Empty Execute method:
            // public override Execute(TCommand command)
            // {
            // }
            // Formatter annotation marks node for formatting according to code style settings     
            var executeMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "Execute"
                )
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier("command")).WithType(commandTypeSyntax)
                            )
                    ))
                .WithBody(SyntaxFactory.Block())
                .WithAdditionalAnnotations(Formatter.Annotation)
                ;

            // When we've got method we can build command handler class.
            var handlerClass = SyntaxFactory
                    .ClassDeclaration($"{commandType.Name}Handler")
                    .WithBaseList(SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(SyntaxFactory.SimpleBaseType(baseType))))
                    .WithMembers(SyntaxFactory.SingletonList<SyntaxNode>(executeMethod));           

            // Next one is namespace
            var ns = SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.ParseName("CommandHandlers"))
                    .WithUsings(SyntaxFactory.SingletonList((UsingDirectiveSyntax)generator.NamespaceImportDeclaration(commandType.ContainingNamespace.ToDisplayString())))
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(handlerClass));

            // Namespace is contained in compilation unit which is root for new document
            var syntaxRoot = SyntaxFactory
                .CompilationUnit()
                    .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(ns));

            // Finally we can add document to proper project
            var handlerDocument = commandHandlersProject.AddDocument(newDocumentName, syntaxRoot);            

            // As Roslyn structures is immutable, we have to return new solution which we can get from added document
            return handlerDocument.Project.Solution;
        }
    }    
}