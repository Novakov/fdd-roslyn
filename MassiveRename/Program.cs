using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Rename;

namespace GenerateCommentsForClasses
{
    class Program
    {
        static void Main(string[] args)
        {
            Do(args).Wait();
        }

        private static async Task Do(string[] args)
        {
            var solutionFile = args[0];

            Console.WriteLine("Opening solution file {0}", solutionFile);

            // Start by creating MSBuild workspace that gives access to Roslyn functionality
            var workspace = MSBuildWorkspace.Create();

            // Then open solution
            var solution = await workspace.OpenSolutionAsync(solutionFile);

            // Rename operation requires some options
            var renameOptions = solution.Workspace.Options
                                .WithChangedOption(RenameOptions.PreviewChanges, false)
                                .WithChangedOption(RenameOptions.RenameInComments, false)
                                .WithChangedOption(RenameOptions.RenameInStrings, false);

            // For simplicity we will be renaming one symbol at the time. After each rename we get new solution. 
            // Tuple (bool, Solution) represents flag if any change has been made and new solution object

            var changedSolution = Tuple.Create(true, solution);
            while (changedSolution.Item1)
            {
                changedSolution = await RenameOneSymbol(changedSolution.Item2, renameOptions);
            }

            // Up to now, changes was only in memory - nothing has been changed on disk. Performing TryApplyChanges on workspace saves them
            if (workspace.TryApplyChanges(changedSolution.Item2))
            {
                Console.WriteLine("Changes applied successfully!");
            }
            else
            {
                Console.WriteLine("Unable to apply changes :(");
            }
        }

        private static async Task<Tuple<bool, Solution>> RenameOneSymbol(Solution solution, OptionSet renameOptions)
        {
            // Go through all project and documents, lookup classes and fields inside them. 
            // If any field needs renaming, perform rename operation
            var changes = from project in solution.Projects
                          from document in project.Documents
                          let root = document.GetSyntaxRootAsync().Result
                          let semantic = document.GetSemanticModelAsync().Result
                          from @class in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                          let classSymbol = semantic.GetDeclaredSymbol(@class)
                          from field in classSymbol.GetMembers().OfType<IFieldSymbol>().Where(x => !x.IsImplicitlyDeclared)
                          where field.Name.StartsWith("_")
                          let newName = field.Name.Substring(1)
                          select Renamer.RenameSymbolAsync(solution, field, newName, renameOptions);

            // Linq is lazy, so nothing has been done yet. Try to get first element (this will start rename) and if it exists, await it and return new solution
            var change = changes.FirstOrDefault();
            if (change != null)
            {
                return Tuple.Create(true, await change);
            }
            else
            {
                return Tuple.Create(false, solution);
            }
        }
    }
}