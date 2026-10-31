using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AccSaber.Tests
{
    /// <summary>
    /// Tests to verify that all classes in the Models directory implement IModel
    /// and use the [UsedImplicitly] annotation.
    /// Note: These tests analyze source code directly since loading the assembly requires Unity dependencies.
    /// </summary>
    public class ModelInterfaceTests
    {
        [Fact]
        public void All_Models_Classes_Should_Implement_IModel()
        {
            var declarations = ReadModelDeclarations(FindModelsDirectory());

            var violations = declarations
                .Where(declaration => declaration.IsClass && !ImplementsIModel(declaration, declarations, new HashSet<string>()))
                .Select(declaration => $"{declaration.FullName} does not implement IModel, inherit from a class that does, or implement an interface derived from IModel")
                .ToArray();

            Assert.True(violations.Length == 0,
                "Found model classes that do not implement IModel:\n" + string.Join("\n", violations));
        }

        private static List<ModelDeclaration> ReadModelDeclarations(string modelsDirectory)
        {
            var declarations = new List<ModelDeclaration>();
            foreach (var file in Directory.GetFiles(modelsDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var source = StripCommentsAndStrings(File.ReadAllText(file));
                var namespaceName = Regex.Match(source, @"\bnamespace\s+([\w.]+)").Groups[1].Value;
                var usings = Regex.Matches(source, @"\busing\s+([\w.]+)\s*;")
                    .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
                var declarationPattern = new Regex(@"\b(class|interface)\s+(\w+)(?:\s*<[^>{}]*>)?");
                foreach (Match match in declarationPattern.Matches(source))
                {
                    var previousSemicolon = source.LastIndexOf(';', match.Index);
                    var previousOpenBrace = source.LastIndexOf('{', match.Index);
                    var previousCloseBrace = source.LastIndexOf('}', match.Index);
                    var previousBoundary = Math.Max(previousSemicolon, Math.Max(previousOpenBrace, previousCloseBrace));
                    var priorText = source.Substring(previousBoundary + 1, match.Index - previousBoundary - 1);
                    // A generic constraint such as `where T : class` is not a type declaration.
                    if (match.Groups[1].Value == "class" && priorText.LastIndexOf("where", StringComparison.Ordinal) >= 0)
                        continue;

                    var headerStart = match.Index + match.Length;
                    var headerEnd = FindDeclarationBodyStart(source, headerStart);
                    if (headerEnd < 0) continue;
                    var header = source[headerStart..headerEnd];
                    var colon = header.IndexOf(':');
                    var where = header.IndexOf("where", StringComparison.Ordinal);
                    var basesText = colon < 0 ? string.Empty : header.Substring(colon + 1, (where < 0 ? header.Length : where) - colon - 1);
                    var bases = SplitBaseTypes(basesText)
                        .Select(RemoveGenericArguments)
                        .ToArray();
                    var name = match.Groups[2].Value;
                    var fullName = string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
                    var declaration = new ModelDeclaration(fullName, name, Path.GetFileName(file), match.Groups[1].Value == "class", bases, usings);
                    // A generic and non-generic type can share a name. Keep all the
                    // declarations so lookup can consider each arity.
                    declarations.Add(declaration);
                }
            }
            return declarations;
        }

        private static IEnumerable<string> SplitBaseTypes(string baseTypes)
        {
            var start = 0;
            var genericDepth = 0;
            for (var index = 0; index < baseTypes.Length; index++)
            {
                if (baseTypes[index] == '<')
                    genericDepth++;
                else if (baseTypes[index] == '>')
                    genericDepth--;
                else if (baseTypes[index] == ',' && genericDepth == 0)
                {
                    yield return baseTypes[start..index].Trim();
                    start = index + 1;
                }
            }

            if (start < baseTypes.Length)
                yield return baseTypes[start..].Trim();
        }

        private static string RemoveGenericArguments(string typeName)
        {
            var genericStart = typeName.IndexOf('<');
            return (genericStart < 0 ? typeName : typeName[..genericStart]).Trim();
        }

        private static string StripCommentsAndStrings(string source)
        {
            return Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/|@""(?:""""|[^""])*""|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'",
                match => new string(match.Value.Select(character => character == '\r' || character == '\n' ? character : ' ').ToArray()));
        }

        private static int FindDeclarationBodyStart(string source, int start)
        {
            var genericDepth = 0;
            for (var index = start; index < source.Length; index++)
            {
                if (source[index] == '<')
                    genericDepth++;
                else if (source[index] == '>')
                    genericDepth--;
                else if (source[index] == '{' && genericDepth == 0)
                    return index;
            }

            return -1;
        }

        private static bool ImplementsIModel(ModelDeclaration declaration, IReadOnlyCollection<ModelDeclaration> declarations, ISet<string> visited)
        {
                var visitKey = declaration.FullName + "|" + string.Join(",", declaration.Bases);
                if (!visited.Add(visitKey))
                return false;

            foreach (var baseType in declaration.Bases)
            {
                var baseName = baseType.Trim();
                if (baseName == "IModel" || baseName.EndsWith(".IModel", StringComparison.Ordinal))
                    return true;

                var separator = declaration.FullName.LastIndexOf('.');
                var namespaceName = separator >= 0 ? declaration.FullName[..separator] : string.Empty;
                var qualifiedName = string.IsNullOrEmpty(namespaceName) ? baseName : namespaceName + "." + baseName;
                if (declarations.Any(candidate => candidate.FullName == qualifiedName && ImplementsIModel(candidate, declarations, new HashSet<string>(visited, StringComparer.Ordinal))))
                    return true;

                var resolvedUsingBase = declaration.Usings.Select(usingName => usingName + "." + baseName)
                    .Select(candidateName => declarations.FirstOrDefault(candidate => candidate.FullName == candidateName))
                    .FirstOrDefault(candidate => candidate != null);
                if (resolvedUsingBase != null && ImplementsIModel(resolvedUsingBase, declarations, new HashSet<string>(visited, StringComparer.Ordinal)))
                    return true;
            }

            return false;
        }

        private sealed class ModelDeclaration
        {
            public ModelDeclaration(string fullName, string name, string fileName, bool isClass, string[] bases, string[] usings)
            {
                FullName = fullName;
                Name = name;
                IsClass = isClass;
                Bases = bases;
                FileName = fileName;
                Usings = usings;
            }

            public string FullName { get; }
            public string Name { get; }
            public bool IsClass { get; }
            public string[] Bases { get; }
            public string FileName { get; }
            public string[] Usings { get; }
        }
        private static string FindModelsDirectory()
        {
            var searchRoots = new[]
            {
                AppContext.BaseDirectory,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Directory.GetCurrentDirectory()
            };

            foreach (var root in searchRoots.Where(path => !string.IsNullOrEmpty(path)))
            {
                var directory = new DirectoryInfo(root!);
                while (directory != null)
                {
                    var candidate = Path.Combine(directory.FullName, "AccSaber", "Models");
                    if (Directory.Exists(candidate))
                        return candidate;

                    candidate = Path.Combine(directory.FullName, "Models");
                    if (Path.GetFileName(directory.FullName).Equals("AccSaber", StringComparison.OrdinalIgnoreCase) && Directory.Exists(candidate))
                        return candidate;

                    directory = directory.Parent;
                }
            }

            throw new DirectoryNotFoundException("Could not locate the AccSaber/Models source directory.");
        }

        [Fact]
        public void All_Models_Classes_Should_Have_UsedImplicitly()
        {
            // Get all C# files in the Models directory
            var modelFiles = Directory.GetFiles(FindModelsDirectory(), "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            foreach (var file in modelFiles)
            {
                var source = StripCommentsAndStrings(File.ReadAllText(file));
                var declarationPattern = new Regex(@"\b(class|interface)\s+(\w+)(?:\s*<[^>{}]*>)?");
                foreach (Match declaration in declarationPattern.Matches(source))
                {
                    if (declaration.Groups[1].Value != "class")
                        continue;

                    var headerStart = declaration.Index;
                    var previousBoundary = Math.Max(source.LastIndexOf(';', headerStart),
                        Math.Max(source.LastIndexOf('{', headerStart), source.LastIndexOf('}', headerStart)));
                    var prefix = source[(previousBoundary + 1)..headerStart];
                    if (Regex.IsMatch(prefix, @"\babstract\s+$"))
                        continue;

                    if (!Regex.IsMatch(prefix, @"\[\s*UsedImplicitly(?:Attribute)?\b"))
                        violations.Add($"{Path.GetFileName(file)}: Class '{declaration.Groups[2].Value}' missing [UsedImplicitly] attribute");
                }
            }

            if (violations.Any())
            {
                var message = "Found Model classes missing [UsedImplicitly] attribute:\n" + string.Join("\n", violations);
                Assert.Fail(message);
            }
        }
    }
}

