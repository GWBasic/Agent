using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;

namespace Agent.Generator
{
    /// <summary>
    /// Entry-point to completing implementing an immutable / mutable pairing class
    /// </summary>
    public abstract class ImmutableCompleter
    {
        public ImmutableCompleter(ISolution solution)
        {
            this.solution = solution;
        }

        public ISolution Solution
        {
            get { return this.solution; }
        }
        private ISolution solution;

        /// <summary>
        /// Inserts completions of immutables and mutables in the given solution
        /// </summary>
        public void Generate()
        {
            var compilation = Compilation.Create("generated");

            var references = new HashSet<MetadataReference>();
            foreach (var project in solution.Projects)
                foreach (var reference in project.MetadataReferences)
                    references.Add(reference);

            compilation = compilation.AddReferences(references);

            var syntaxTrees = new List<Roslyn.Compilers.Common.CommonSyntaxTree>();
            foreach (var project in solution.Projects)
                foreach (var document in project.Documents)
                    syntaxTrees.Add(document.GetSyntaxTree());

            var commonCompilation = compilation.AddSyntaxTrees(syntaxTrees);
            
            foreach (var project in this.Solution.Projects)
            {
                Console.WriteLine("\tIterating through {0}", project.Name);



                foreach (var codeDocument in from codeDocument in project.Documents
                                             where
                                                 codeDocument.Name.EndsWith(".cs")
                                             select codeDocument)
                {
                    Console.WriteLine("\t\tInspecting {0}", codeDocument.FilePath);

                    this.InspectAndUpdate(codeDocument, commonCompilation);
                }
            }
        }


        /// <summary>
        /// Inspects the given code document to see if there's any need for immutable / mutable generation
        /// </summary>
        /// <param name="codeDocument"></param>
        /// <param name="text"></param>
        private void InspectAndUpdate(IDocument codeDocument, Roslyn.Compilers.Common.CommonCompilation commonCompilation)
        {
            // mild premature optimization
            var text = codeDocument.GetText().GetText();
            if (text.Contains("immutable_generated") && text.Contains("immutable_declarations"))
            {
                var updateRegions = new List<UpdateRegion>();

                // Compile
                var syntax = codeDocument.GetSyntaxTree();
                var root = syntax.GetRoot();

                // Filter down to classes
                foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var immutableGeneratedTrivia = this.GetRegion(classDeclaration, "immutable_generated");

                    if (null != immutableGeneratedTrivia)
                    {
                        var className = classDeclaration.Identifier.ValueText;
                        var classFullName = className;

                        var parent = classDeclaration.Parent;
                        while (null != parent)
                        {
                            if (parent is NamespaceDeclarationSyntax)
                                classFullName = ((NamespaceDeclarationSyntax)parent).Name.ToString().Trim() + "." + classFullName;

                            parent = parent.Parent;
                        }

                        var compiledClass = commonCompilation.GetTypeByMetadataName(classFullName);

                        var updateRegion = new UpdateRegion()
                        {
                            begin = immutableGeneratedTrivia.beginRegionTrivia.FullSpan.End,
                            end = immutableGeneratedTrivia.endRegionTrivia.FullSpan.Start
                        };

                        try
                        {
                            if (null != compiledClass)
                            {
                                Console.WriteLine("\t\t\t{0} is an immutable", classDeclaration.Identifier.GetText());

                                var immutableDeclarationsTrivia = this.GetRegion(classDeclaration, "immutable_declarations");

                                if (null != immutableDeclarationsTrivia)
                                {
                                    var fields = new List<IFieldSymbol>();

                                    // Iterate through all the tokens in the immutable_declarations to figure out the fields
                                    var token = immutableDeclarationsTrivia.beginRegionTrivia.Token;
                                    while (token != immutableDeclarationsTrivia.endRegionTrivia.Token)
                                    {
                                        if (token.Kind == SyntaxKind.IdentifierToken)
                                        {
                                            // Get the compiled version of the identifies
                                            var identifier = token.ValueText;
                                            var field = compiledClass.GetMembers(identifier)
                                                .FirstOrDefault() as IFieldSymbol;

                                            // Verify it
                                            if (null == field)
                                                throw new Exception(string.Format(
                                                    "{0} must be a readonly field",
                                                    identifier));

                                            if (field.IsStatic)
                                                throw new Exception(string.Format(
                                                    "Can not wrap static fields: {0}",
                                                    identifier));

                                            if (!field.IsReadOnly)
                                                throw new Exception(string.Format(
                                                    "Exposed fields must be readonly: {0}",
                                                    identifier));

                                            if (field.HasConstantValue)
                                                throw new Exception(string.Format(
                                                    "Exposed fields not be constant: {0}",
                                                    identifier));

                                            // TODO: Need to verify that the field is private
                                            //if (field.

                                            fields.Add(field);
                                        }

                                        token = token.GetNextToken();
                                    }

                                    var builder = new StringBuilder();
                                    builder.AppendLine();
                                    builder.Append(immutableGeneratedTrivia.whiteSpace);
                                    builder.AppendLine("// Generated code, do not edit unless you know what you're doing!");

                                    var propertyNames = new Dictionary<IFieldSymbol, string>();

                                    // Generate properties and set mutators
                                    foreach (var field in fields)
                                    {
                                        var type = field.Type.ToDisplayString();
                                        
                                        // Determine property name
                                        string propertyName;
                                        if (field.Name.StartsWith("_"))
                                            propertyName = field.Name.Substring(1);
                                        else
                                        {
                                            var firstChar = field.Name[0].ToString();
                                            var firstCharU = firstChar.ToUpperInvariant();
                                            var firstCharL = firstChar.ToLowerInvariant();

                                            if (firstCharL == firstCharU)
                                            {
                                                propertyName = field.Name.ToUpperInvariant();

                                                if (propertyName == field.Name)
                                                    propertyName = field.Name.ToLowerInvariant();

                                                if (propertyName == field.Name)
                                                    propertyName = field.Name + "_Property";
                                            }
                                            else
                                            {
                                                propertyName = field.Name.Substring(1);
                                                if (firstCharL == firstChar)
                                                    propertyName = firstCharU + propertyName;
                                                else
                                                    propertyName = firstCharL + propertyName;
                                            }
                                        }

                                        propertyNames[field] = propertyName;

                                        // Property
                                        builder.AppendFormat(
                                            "{0}public {1} {2} {{ get {{ return this.{3}; }}}}",
                                            immutableGeneratedTrivia.whiteSpace,
                                            type,
                                            propertyName,
                                            field.Name);

                                        builder.AppendLine();

                                        // Set Mutator
                                        builder.AppendFormat(
                                            "{0}public {1} Set{2}({3} {4}) {{ return new {1}(",
                                            immutableGeneratedTrivia.whiteSpace,
                                            className,
                                            propertyName,
                                            type,
                                            field.Name);

                                        var passes = new List<string>();
                                        foreach (var subField in fields)
                                        {
                                            if (subField == field)
                                                passes.Add(field.Name);
                                            else
                                                passes.Add("this." + subField.Name);
                                        }

                                        builder.Append(string.Join(", ", passes.ToArray()));
                                        builder.AppendLine("); }");

                                        builder.AppendLine();
                                    }

                                    builder.AppendLine();

                                    // Generate constructor
                                    builder.Append(immutableDeclarationsTrivia.whiteSpace);
                                    builder.AppendFormat("public {0}(", className);

                                    var argumentDeclarations = new List<string>();
                                    foreach (var field in fields)
                                    {
                                        argumentDeclarations.Add(string.Format(
                                            "{0} {1} = default({0})",
                                            field.Type.ToDisplayString(),
                                            field.Name));
                                    }

                                    builder.Append(string.Join(", ", argumentDeclarations.ToArray()));
                                    builder.Append(") { ");

                                    foreach (var field in fields)
                                    {
                                        builder.AppendFormat("this.{0} = {0}; ", field.Name);
                                    }

                                    builder.AppendLine("}");
                                    builder.AppendLine();

                                    // Mutable class
                                    builder.Append(immutableGeneratedTrivia.whiteSpace);
                                    builder.Append("public class Mutable {");

                                    foreach (var field in fields)
                                        builder.AppendFormat(
                                            " public {0} {1} {{ get; set; }}",
                                            field.Type.ToDisplayString(),
                                            propertyNames[field]);

                                    // Generate immutable from mutable
                                    builder.AppendFormat(
                                        " public {0} ToImmutable() {{ return new {0}(",
                                        className);

                                    var assignments = new List<string>(fields.Count);
                                    foreach (var field in fields)
                                        assignments.Add(string.Format(
                                            "this.{0}",
                                            propertyNames[field]));

                                    builder.Append(string.Join(", ", assignments.ToArray()));
                                    builder.AppendLine(");} }");

                                    // GenerateMutable method
                                    builder.AppendFormat(
                                        "{0}public Mutable ToMutable() {{ return new Mutable() {{ ",
                                        immutableGeneratedTrivia.whiteSpace);

                                    assignments = new List<string>(fields.Count);
                                    foreach (var field in fields)
                                        assignments.Add(string.Format(
                                            "{0} = this.{1}",
                                            propertyNames[field],
                                            field.Name));

                                    builder.Append(string.Join(", ", assignments.ToArray()));
                                    builder.AppendLine("}; }");

                                    builder.AppendLine();

                                    updateRegion.newText = builder.ToString();
                                }
                                else
                                {
                                    updateRegion.newText = immutableGeneratedTrivia.whiteSpace + "// Missing #region immutable_declarations\n\r";
                                }
                            }
                            else
                            {
                                updateRegion.newText = immutableGeneratedTrivia.whiteSpace + "// " + classFullName + " does not compile\n\r";
                            }
                        }
                        catch (Exception e)
                        {
                            updateRegion.newText = string.Format("/*{0}*/\r\n", e);
                        }

                        updateRegions.Add(updateRegion);
                    }
                }

                if (updateRegions.Count > 0)
                    this.Update(codeDocument, text, updateRegions);
            }
        }

        private class BeginEndRegionTrivia
        {
            public SyntaxTrivia beginRegionTrivia;
            public SyntaxTrivia endRegionTrivia;
            public string whiteSpace;
        }

        private BeginEndRegionTrivia GetRegion(ClassDeclarationSyntax classDeclaration, string name)
        {
            using (var triviaEnumerator = classDeclaration.DescendantTrivia().GetEnumerator())
            while (triviaEnumerator.MoveNext())
            {
                var beginRegionTrivia = triviaEnumerator.Current;

                if (SyntaxKind.RegionDirective == beginRegionTrivia.Kind)
                {
                    var text = beginRegionTrivia.GetText().Trim();

                    if (text.Trim().EndsWith(name))
                    {
                        // The #region is found, now looking for the matching #endregion
                        var prev = beginRegionTrivia;
                        var depth = 0;
                        while (triviaEnumerator.MoveNext())
                        {
                            var endRegionTrivia = triviaEnumerator.Current;

                            // Handle nested #regions...
                            if (SyntaxKind.RegionDirective == endRegionTrivia.Kind)
                            {
                                depth++;
                            }
                            else if (SyntaxKind.EndRegionDirective == endRegionTrivia.Kind)
                            {
                                if (0 == depth)
                                    return new BeginEndRegionTrivia()
                                    {
                                        beginRegionTrivia = beginRegionTrivia,
                                        endRegionTrivia = prev, // this places the text within the whitespace
                                        whiteSpace = prev.GetText()
                                    };
                                else
                                    depth--;
                            }

                            prev = endRegionTrivia;
                        }
                    }
                }
            }

            // No matching #region and #endregion found
            return null;
        }

        protected class UpdateRegion
        {
            public int begin;
            public int end;
            public string newText;
        }

        private void Update(IDocument document, string text, List<UpdateRegion> updateRegions)
        {
            // Updates are performed by building a list of strings to piece together
            var overestimatedLength = text.Length;
            foreach (var updateRegion in updateRegions)
                overestimatedLength += updateRegion.newText.Length;

            var newTextBuilder = new StringBuilder(overestimatedLength);

            // BIG TODO
            // Instead of string manipulation, try to manipulate using the API


            // sort in order of regions
            updateRegions.Sort((a, b) => a.begin - b.begin);

            // build up the strings
            var prev = new UpdateRegion()
            {
                end = 0
            };

            foreach (var updateRegion in updateRegions)
            {
                var substring = text.Substring(prev.end, updateRegion.begin - prev.end);
                newTextBuilder.Append(substring);
                newTextBuilder.Append(updateRegion.newText);

                prev = updateRegion;
            }

            newTextBuilder.Append(text.Substring(prev.end, text.Length - prev.end));

            // perform the swap via the ID
            var newText = newTextBuilder.ToString();
            Console.WriteLine("\t\t\t\tNew text\n---\n{0}\n---\n", newText);

            // Again, this should be optimized by not performing string manipulation and manipulating the tree
            var recompiled = SyntaxTree.ParseCompilationUnit(newText);
            this.solution = this.solution.UpdateDocument(document.Id, recompiled.GetRoot());

            this.Update(document, newText);
       }

        protected abstract void Update(IDocument document, string newText);
    }
}
