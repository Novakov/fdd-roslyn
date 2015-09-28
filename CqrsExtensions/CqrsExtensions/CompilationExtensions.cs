using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CqrsExtensions
{
    static class CompilationExtensions
    {
        public static ISymbol CommandBaseTypeSymbol(this Compilation @this)
        {
            return @this.GetSymbolsWithName(x => x == "Command", SymbolFilter.Type)
                .SingleOrDefault(x => x.ContainingNamespace?.Name == "Commands");
        }

        public static INamedTypeSymbol CommandHandlerBaseTypeSymbol(this Project @this)
        {
            return SymbolFinder.FindDeclarationsAsync(@this, "CommandHandler", false, SymbolFilter.Type).Result
                .OfType<INamedTypeSymbol>()
                .Single(x => x.ContainingNamespace?.Name == "CommandHandlers");
        }
    }
}
