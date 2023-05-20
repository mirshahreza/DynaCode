using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.ComponentModel;
using System.Reflection;

namespace DynaCode
{

    public static class DynaCompiler
    {
        private static bool codeIncluded = true;
        public static bool CodeIncluded
        {
            get
            {
                return codeIncluded;
            }
        }

        private static bool isDevelopment = true;
        public static bool IsDevelopment
        {
            get
            {
                return isDevelopment;
            }
        }

        private static string? startPath;
        public static string StartPath
        {
            get
            {
                if (startPath is null) throw new Exception("Start path is not configured yet :|");
                return startPath;
            }
            set
            {
                startPath = value;
            }
        }

        public static void Init(string dynaCodeStartPath, bool dynaCodeIncluded = false, bool isDevelopmentArea = false)
        {
            StartPath = dynaCodeStartPath;
            codeIncluded = dynaCodeIncluded;
            isDevelopment = isDevelopmentArea;
        }

        private static string? asmPath;
        private static string AsmPath
        {
            get
            {
                if (asmPath is null) asmPath = $"DynaAsm{Guid.NewGuid().ToString().Replace("-", "")}.dll";
                return asmPath;
            }
        }

        private static Assembly? dynaAsm;
        public static Assembly DynaAsm
        {
            get
            {
                if (dynaAsm == null)
                {
                    if (!File.Exists(AsmPath)) Build();
                    dynaAsm = Assembly.LoadFrom(AsmPath);
                }
                return dynaAsm;
            }
        }
        public static bool Build()
        {
            using var peStream = new MemoryStream();
            List<string> sourceCodes = GetAllSourceCodes();
            var result = GenerateCSharpCompilation(sourceCodes).Emit(peStream);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var error = failures.FirstOrDefault();
                throw new Exception($"{error?.Id}: {error?.GetMessage()}");
            }

            peStream.Seek(0, SeekOrigin.Begin);
            byte[] dllBytes = peStream.ToArray();

            File.WriteAllBytes(AsmPath, dllBytes);

            return true;
        }

        private static List<string> GetAllSourceCodes()
        {
            string[] scriptFiles = DynaUtils.GetFiles(StartPath, "*.cs").ToArray();
            List<string> sourceCodes = new List<string>();
            foreach (string f in scriptFiles)
            {
                string s = File.ReadAllText(f);
                sourceCodes.Add(s);
            }
            return sourceCodes;
        }

        private static CSharpCompilation GenerateCSharpCompilation(List<string> sourceFiles)
        {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
            var parsedSyntaxTrees = sourceFiles.Select(f => SyntaxFactory.ParseSyntaxTree(f, options));
            var compileRefs = GetCompilationReferences();
            var compilerOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);
            return CSharpCompilation.Create(AsmPath, parsedSyntaxTrees, compileRefs, compilerOptions);
        }
        private static List<MetadataReference> GetCompilationReferences()
        {
            var refs = new List<MetadataReference>();
            var executingReferences = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
            refs.AddRange(executingReferences.Select(a => MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            var entryAsm = Assembly.GetEntryAssembly();
            if (entryAsm != null)
            {
                var entryReferences = entryAsm.GetReferencedAssemblies();
                refs.AddRange(entryReferences.Select(a => MetadataReference.CreateFromFile(Assembly.Load(a).Location)));
            }

            refs.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(TypeConverter).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard, Version=2.1.0.0").Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(System.Data.Common.DbConnection).Assembly.Location));
            refs.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Expressions.Expression).Assembly.Location));

            return refs;
        }


        public static void Refresh()
        {
            asmPath = null;
            dynaAsm = null;
        }

    }
}