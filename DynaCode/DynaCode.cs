using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.ComponentModel;
using System.Reflection;
using static System.Text.Json.JsonElement;
using System.Text.Json;
using System.Text;

namespace AppEnd
{
    public static class DynaCode
    {
        private static bool codeIncluded = false;
        public static bool CodeIncluded
        {
            get
            {
                return codeIncluded;
            }
        }

        private static bool isDevelopment = false;
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

        public static void Init(string dynaCodeStartPath, bool dynaCodeIncluded = false, bool isDevelopmentArea = false)
        {
            startPath = dynaCodeStartPath;
            codeIncluded = dynaCodeIncluded;
            isDevelopment = isDevelopmentArea;
        }
        public static void Refresh()
        {
            asmPath = null;
            dynaAsm = null;
        }
        public static object? CodeInvode(string methodFullPath, JsonElement inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return methodInfo.Invoke(null, ExtractParams(methodInfo, inputParams));
        }
        public static object? CodeInvode(string typeName, string methodName, JsonElement inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(null, typeName, methodName);
            return methodInfo.Invoke(null, ExtractParams(methodInfo, inputParams));
        }
        public static object? CodeInvode(string nameSpaceName, string typeName, string methodName, JsonElement inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(nameSpaceName, typeName, methodName);
            return methodInfo.Invoke(null, ExtractParams(methodInfo, inputParams));
        }
        public static object? CodeInvode(string typeName, string methodName, object[] inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(null, typeName, methodName);
            return methodInfo.Invoke(null, inputParams);
        }
        public static object? CodeInvode(string methodFullPath, object[] inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return methodInfo.Invoke(null, inputParams);
        }
        public static object? CodeInvode(string? nameSpaceName, string typeName, string methodName, object[] inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(nameSpaceName, typeName, methodName);
            return methodInfo.Invoke(null, inputParams);
        }

        

        private static bool Build()
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
            string[] scriptFiles = GetFiles(StartPath, "*.cs").ToArray();
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

        private static object[] ExtractParams(MethodInfo methodInfo, JsonElement jsonElement)
        {
            List<object> methodInputs = new List<object>();
            ParameterInfo[] methodParams = methodInfo.GetParameters();
            ObjectEnumerator objects = jsonElement.EnumerateObject();

            foreach (var paramInfo in methodParams)
            {
                IEnumerable<JsonProperty> l = objects.Where(i => string.Equals(i.Name, paramInfo.Name));
                if (l.Count() == 0) throw new Exception($"Input params for [ {methodInfo.GetFullName()} ] does not contains parameter named [ {paramInfo.Name} ]");
                JsonProperty p = l.First();
                methodInputs.Add(p.Value.ToOrigType(paramInfo));
            }

            return methodInputs.ToArray();
        }
        private static object ToOrigType(this JsonElement s, ParameterInfo parameterInfo)
        {
            if (parameterInfo.ParameterType == typeof(string)) return s.ToString();
            if (parameterInfo.ParameterType == typeof(int)) return int.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int16)) return Int16.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int32)) return Int32.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int64)) return Int64.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(bool)) return bool.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(DateTime)) return DateTime.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Guid)) return Guid.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(float)) return float.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Decimal)) return Decimal.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(byte[])) return Encoding.UTF8.GetBytes(s.ToString());
            return s;
        }
        private static MethodInfo GetMethodInfo(string methodFullPath)
        {
            if (methodFullPath.Trim() == "") throw new Exception($"{methodFullPath} can not be empty");
            string[] parts = methodFullPath.Trim().Split('.');
            if (parts.Length < 2 || parts.Length > 3) throw new Exception($"Requested method [{methodFullPath}] does not exist.");
            return parts.Length == 3 ? GetMethodInfo(parts[0], parts[1], parts[2]) : GetMethodInfo(null, parts[0], parts[1]);
        }
        private static MethodInfo GetMethodInfo(string? nameSpaceName, string typeName, string methodName)
        {
            if (typeName.Trim() == "") throw new Exception($"The TypeName can not be empty");
            string tn = nameSpaceName is null || nameSpaceName == "" ? typeName : nameSpaceName + "." + typeName;
            MethodInfo? methodInfo = GetType(tn).GetMethod(methodName);
            if (methodInfo == null) throw new Exception($"Requested method [{methodName}] does not exist.");
            return methodInfo;
        }
        private static Type GetType(string typeFullName)
        {
            string tName = typeFullName;
            string nsName = "";
            Type? dynamicType;
            if (typeFullName.Contains("."))
            {
                nsName = typeFullName.Split('.')[0];
                tName = typeFullName.Split(".")[1];
            }
            if (IsDevelopment || CodeIncluded)
            {
                dynamicType = Assembly.GetEntryAssembly()?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                if (dynamicType == null)
                {
                    dynamicType = DynaAsm?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                }
            }
            else
            {
                dynamicType = DynaAsm?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                if (dynamicType == null)
                {
                    dynamicType = Assembly.GetEntryAssembly()?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                }
            }
            if (dynamicType == null) throw new Exception($"Requested type [ {typeFullName} ] does not exist.");
            return dynamicType;
        }
        private static IEnumerable<string> GetFiles(string path, string searchPattern)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                foreach (string subDir in Directory.GetDirectories(path))
                {
                    queue.Enqueue(subDir);
                }
                string[] files = Directory.GetFiles(path, searchPattern);
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }
        private static string GetFullName(this MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType is not null)
                return methodInfo.DeclaringType.Namespace + "." + methodInfo.DeclaringType.Name + "." + methodInfo.Name;
            else
                return methodInfo.Name;
        }
        public static void AddExampleCode()
        {
            File.WriteAllText(StartPath + "/Example.cs", @"
namespace Example
{
    public static class ExampleT
    {
        public static int ExampleM(int a, int b)
        {
            return a + b;
        }
    }
}

");
        }
        public static void RemoveExampleCode()
        {
            if (File.Exists(StartPath + "/Example.cs"))
                File.Delete(StartPath + "/Example.cs");
        }
    }
}