using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tickwork.SourceGenerator.Validation
{
    /// <summary>
    /// Handles validation of constructor configurations for TickerFunction classes.
    /// </summary>
    internal static class ConstructorValidator
    {
        /// <summary>
        /// Validates that a class doesn't have multiple constructors.
        /// Issues a warning if multiple constructors are found and no TickworkConstructor attribute is present.
        /// Issues an error if multiple constructors have TickworkConstructor attribute.
        /// </summary>
        public static void ValidateMultipleConstructors(
            ClassDeclarationSyntax classDeclaration,
            SemanticModel semanticModel,
            SourceProductionContext context)
        {
            var constructors = classDeclaration.Members.OfType<ConstructorDeclarationSyntax>().ToList();
            var hasPrimaryConstructor = classDeclaration.ParameterList?.Parameters.Count > 0;
            
            // Count total constructors (regular + primary)
            var totalConstructors = constructors.Count + (hasPrimaryConstructor ? 1 : 0);
            
            // Check for TickworkConstructor attributes
            var constructorsWithTickworkAttribute = new List<ConstructorDeclarationSyntax>();
            
            foreach (var constructor in constructors)
            {
                var constructorSymbol = semanticModel.GetDeclaredSymbol(constructor);
                if (constructorSymbol != null)
                {
                    var hasTickworkAttribute = constructorSymbol.GetAttributes().Any(attr =>
                    {
                        var attributeClass = attr.AttributeClass;
                        if (attributeClass == null) return false;
                        
                        var attributeName = attributeClass.Name;
                        var fullName = attributeClass.ToDisplayString();
                        
                        return attributeName == "TickworkConstructorAttribute" || 
                               attributeName == "TickworkConstructor" ||
                               fullName == "Tickwork.Utilities.TickworkConstructorAttribute" ||
                               fullName == "Tickwork.Utilities.TickworkConstructor";
                    });
                    
                    if (hasTickworkAttribute)
                    {
                        constructorsWithTickworkAttribute.Add(constructor);
                    }
                }
            }
            
            // Error if multiple constructors have TickworkConstructor attribute
            if (constructorsWithTickworkAttribute.Count > 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleTickworkConstructorAttributes,
                    classDeclaration.Identifier.GetLocation(),
                    classDeclaration.Identifier.Text
                ));
            }
            // Warning if multiple constructors exist but no TickworkConstructor attribute
            else if (totalConstructors > 1 && constructorsWithTickworkAttribute.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleConstructors,
                    classDeclaration.Identifier.GetLocation(),
                    classDeclaration.Identifier.Text
                ));
            }
        }
    }
}
