using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.ComponentModel;
using System.Reflection;
using static System.Text.Json.JsonElement;
using System.Text.Json;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.AccessControl;
using System.Diagnostics;

namespace AppEnd
{
    public static class DynaCode
    {
        private static CodeInvokeOptions invokeOptions;

        private static IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        public static IMemoryCache MemoryCache
        {
            get
            {
                return memoryCache;
            }
        }
        private static IEnumerable<SyntaxTree>? entierCodeSyntaxes;
        private static IEnumerable<SyntaxTree> EntierCodeSyntaxes
        {
            get
            {
                if (entierCodeSyntaxes is null)
                {
                    List<SourceCode> sourceCodes = GetAllSourceCodes();
                    var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
                    entierCodeSyntaxes = sourceCodes.Select(sourceCode => SyntaxFactory.ParseSyntaxTree(sourceCode.RawCode, options, sourceCode.FilePath));
                }
                return entierCodeSyntaxes;
            }
        }

        private static string[]? scriptFiles;
        public static string[] ScriptFiles
        {
            get
            {
                if(scriptFiles is null)
                {
                    scriptFiles = Utils.GetFiles(invokeOptions.StartPath, "*.cs").ToArray();
                }
                return scriptFiles;
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

        private static List<CodeMap> codeMaps;
        public static List<CodeMap> CodeMaps
        {
            get
            {
                if (codeMaps is null)
                {
                    codeMaps = GenerateSourceCodeMap();
                }
                return codeMaps;
            }
        }

        public static void Init(CodeInvokeOptions codeInvokeOptions)
        {
            invokeOptions = codeInvokeOptions;
            EnsureLogFolders();
            Refresh();
        }
        public static void Refresh()
        {
            string[] oldAsmFiles =  Directory.GetFiles(".", "DynaAsm*");
            foreach(string oldAsmFile in oldAsmFiles)
            {
                try
                {
                    File.Delete(oldAsmFile);
                }
                catch { }
            }
            entierCodeSyntaxes = null;
            scriptFiles = null;
            asmPath = null;
            dynaAsm = null;
        }
        
        public static CodeInvokeResult InvokeByJsonInputs(string methodFullPath, JsonElement? inputParams = null, DynaUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return Invoke(methodInfo, ExtractParams(methodInfo, inputParams), dynaUser, clientInfo);
        }
        public static CodeInvokeResult InvokeByParamsInputs(string methodFullPath, object[]? inputParams = null, DynaUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return Invoke(methodInfo, inputParams, dynaUser, clientInfo);
        }

        private static CodeInvokeResult Invoke(MethodInfo methodInfo, object[]? inputParams = null, DynaUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            MethodSettings methodSettings = ReadMethodSettings(methodInfo.GetFullName());
            if (methodSettings.CachePolicy != null && methodSettings.CachePolicy.CacheLevel == CacheLevel.PerUser && (dynaUser is null || dynaUser.UserName.Trim() == ""))
                throw new ArgumentNullException($"CachePolicy.CacheLevel for {methodInfo.GetFullName()} is set to PerUser but the current user is null");

            CodeInvokeResult codeInvokeResult;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                CheckAccess(methodInfo, methodSettings, dynaUser);
                string cacheKey = CalculateCacheKey(methodInfo, methodSettings, inputParams, dynaUser);
                object? result;
                if (methodSettings.CachePolicy?.CacheLevel != CacheLevel.None && memoryCache.TryGetValue(cacheKey, out result) && ignoreCaching == false)
                {
                    stopwatch.Stop();
                    codeInvokeResult = new() { Result = result, FromCache = true, IsSucceeded = true, Duration = stopwatch.ElapsedMilliseconds };
                }
                else
                {
                    try
                    {
                        result = methodInfo.Invoke(null, inputParams);
                        if (methodSettings.CachePolicy?.CacheLevel != CacheLevel.None)
                        {
                            MemoryCacheEntryOptions cacheEntryOptions = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(methodSettings.CachePolicy.AbsoluteExpirationSeconds) };
                            memoryCache.Set(cacheKey, result, cacheEntryOptions);
                        }
                        stopwatch.Stop();
                        codeInvokeResult = new() { Result = result, FromCache = false, IsSucceeded = true, Duration = stopwatch.ElapsedMilliseconds };
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        codeInvokeResult = new() { Result = ex, FromCache = null, IsSucceeded = false, Duration = stopwatch.ElapsedMilliseconds };
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                codeInvokeResult = new() { Result = ex, FromCache = null, IsSucceeded = false, Duration = stopwatch.ElapsedMilliseconds };
            }


            LogMethodInvoke(methodInfo, methodSettings, codeInvokeResult, inputParams, dynaUser, clientInfo);
            return codeInvokeResult;
        }

        private static void CheckAccess(MethodInfo methodInfo,MethodSettings methodSettings, DynaUser? dynaUser)
        {
            if (dynaUser is null) return;
            if (dynaUser.UserName.ToLower() == invokeOptions.PublicKeyUser.ToLower()) return;
            if (dynaUser.Roles.Contains(invokeOptions.PublicKeyRole)) return;
            if (dynaUser.Roles.ToList().Exists(i => methodSettings.AccessRules.AllowedRoles.Contains(i))) return;
            if (methodSettings.AccessRules.AllowedRoles.Contains("*")) return;
            if (methodSettings.AccessRules.AllowedUsers.Contains(dynaUser.UserName)) return;
            if (methodSettings.AccessRules.AllowedUsers.Contains("*")) return;
            throw new Exception($"Access denied, The user [ {dynaUser.UserName} ] doesn't have enough access to execute [ {methodInfo.GetFullName()} ]");
        }
        private static void LogMethodInvoke(MethodInfo methodInfo, MethodSettings methodSettings, CodeInvokeResult codeInvokeResult, object[]? inputParams,DynaUser? dynaUser,string clientInfo = "")
        {
            string logMethod;
            if (codeInvokeResult.IsSucceeded)
            {
                if (methodSettings.LogPolicy.OnSuccessLogMethod == "") return;
                logMethod = methodSettings.LogPolicy.OnSuccessLogMethod;
            }
            else
            {
                if (methodSettings.LogPolicy.OnErrorLogMethod == "") return;
                logMethod = methodSettings.LogPolicy.OnErrorLogMethod;
            }

            if (methodInfo.GetFullName().ToLower() == logMethod.ToLower()) return;

            List<object> list = new List<object>
            {
                methodInfo,
                invokeOptions.LogFolderPath,
                dynaUser is null ? "" : dynaUser.UserName,
                methodInfo.GetFullName(),
                inputParams,
                codeInvokeResult,
                clientInfo
            };
            GetMethodInfo(logMethod).Invoke(null, list.ToArray());
        }

        public static void WriteMethodSettings(string methodFullName, MethodSettings methodSettings)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName == methodFullName);
            if (codeMap is null) throw new Exception($"{methodFullName} is not exist");
            string settingsFileName = codeMap.FilePath + ".settings.json";
            string settingsRaw = File.Exists(settingsFileName) ? File.ReadAllText(settingsFileName) : "{}";
            JsonNode? jsonNode = JsonNode.Parse(settingsRaw);
            if (jsonNode is null) throw new Exception("Unknow Error");
            jsonNode[codeMap.MethodFullName] = JsonNode.Parse(methodSettings.Serialize());
            File.WriteAllText(settingsFileName, jsonNode.ToString());
        }
        public static MethodSettings ReadMethodSettings(string methodFullName)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName == methodFullName);
            if (codeMap is null) throw new Exception($"{methodFullName} is not exist");
            string settingsFileName = codeMap.FilePath + ".settings.json";
            string settingsRaw = File.Exists(settingsFileName) ? File.ReadAllText(settingsFileName) : "{}";
            try
            {
                var jsonNode = JsonNode.Parse(settingsRaw);
                if (jsonNode is null) throw new Exception("Unknow Error");
                if (jsonNode[codeMap.MethodFullName] == null) return new();
                MethodSettings? methodSettings = jsonNode[codeMap.MethodFullName].Deserialize<MethodSettings>(options: new() { IncludeFields = true });
                if (methodSettings is null) return new();
                return methodSettings;
            }
            catch
            {
                throw new InvalidCastException($"Settings for [ {codeMap.MethodFullName} ] stored in the file [ {codeMap.FilePath} ] is not valid");
            }
        }
        public static void RemoveMethodSettings(string methodFullName)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName == methodFullName);
            if (codeMap is null) throw new Exception($"{methodFullName} is not exist");
            string settingsFileName = codeMap.FilePath + ".settings.json";
            if (!File.Exists(settingsFileName)) return;
            File.Delete(settingsFileName);
        }

        private static void Build()
        {
            using var peStream = new MemoryStream();

            var compileRefs = GetCompilationReferences();
            var compilerOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);
            CSharpCompilation cSharpCompilation = CSharpCompilation.Create(AsmPath, EntierCodeSyntaxes, compileRefs, compilerOptions);

            var result = cSharpCompilation.Emit(peStream);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                var error = failures.FirstOrDefault();
                throw new Exception($"{error?.Id}: {error?.GetMessage()}");
            }

            peStream.Seek(0, SeekOrigin.Begin);
            byte[] dllBytes = peStream.ToArray();

            File.WriteAllBytes(AsmPath, dllBytes);
        }

        private static List<SourceCode> GetAllSourceCodes()
        {
            List<SourceCode> sourceCodes = new List<SourceCode>();
            foreach (string f in ScriptFiles)
            {
                sourceCodes.Add(new() { FilePath = f, RawCode = File.ReadAllText(f) });
            }
            return sourceCodes;
        }
        private static List<MetadataReference> GetCompilationReferences()
        {
            var references = new List<MetadataReference>();

            AddReferencesFor(Assembly.GetExecutingAssembly(), references);
            AddReferencesFor(Assembly.GetEntryAssembly(), references);
            AddReferencesFor(typeof(object).Assembly, references);
            AddReferencesFor(typeof(TypeConverter).Assembly, references);
            AddReferencesFor(Assembly.Load("netstandard, Version=2.1.0.0"), references);
            AddReferencesFor(typeof(System.Linq.Expressions.Expression).Assembly, references);

            return references;
        }
        private static void AddReferencesFor(Assembly? asm, List<MetadataReference> references)
        {
            if (asm is not null)
            {
                references.Add(MetadataReference.CreateFromFile(asm.Location));
                var entryReferences = asm.GetReferencedAssemblies();
                references.AddRange(entryReferences.Select(a => MetadataReference.CreateFromFile(Assembly.Load(a).Location)));
            }
        }


        private static List<CodeMap> GenerateSourceCodeMap()
        {
            List<CodeMap> codeMaps = new List<CodeMap>();
            foreach (var st in EntierCodeSyntaxes)
            {
                var members = st.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>();
                foreach (var member in members)
                {
                    if (member is MethodDeclarationSyntax method)
                    {
                        string nsn = "";
                        SyntaxNode? parentNameSpace = ((ClassDeclarationSyntax)method.Parent).Parent;
                        if (parentNameSpace is not null) nsn = ((NamespaceDeclarationSyntax)parentNameSpace).Name.ToString() + ".";
                        string tn = ((ClassDeclarationSyntax)method.Parent).Identifier.ValueText + ".";
                        string mn = method.Identifier.ValueText;
                        codeMaps.Add(new() { FilePath = st.FilePath, MethodFullName = nsn + tn + mn });
                    }
                }

            }
            return codeMaps;
        }

        private static object[]? ExtractParams(MethodInfo methodInfo, JsonElement? jsonElement)
        {
            if (jsonElement is null) return null;
            List<object> methodInputs = new List<object>();
            ParameterInfo[] methodParams = methodInfo.GetParameters();
            ObjectEnumerator objects = ((JsonElement)jsonElement).EnumerateObject();

            foreach (var paramInfo in methodParams)
            {
                IEnumerable<JsonProperty> l = objects.Where(i => string.Equals(i.Name, paramInfo.Name));
                if (l.Count() == 0) throw new Exception($"Input params for [ {methodInfo.GetFullName()} ] does not contains parameter named [ {paramInfo.Name} ]");
                JsonProperty p = l.First();
                methodInputs.Add(p.Value.ToOrigType(paramInfo));
                //methodInputs.Add(p.Value);
            }
            return methodInputs.ToArray();
        }
        private static MethodInfo GetMethodInfo(string methodFullPath)
        {
            if (methodFullPath.Trim() == "") throw new Exception($"{methodFullPath} can not be empty");
            string[] parts = methodFullPath.Trim().Split('.');
            if (parts.Length < 2 || parts.Length > 3) throw new Exception($"Requested method [{methodFullPath}] must contains at least 2 parts separated by dot[.] symbol.");
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
            if (invokeOptions.IsDevelopment || invokeOptions.CompiledIn)
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
        public static void AddExampleCode()
        {
            File.WriteAllText(invokeOptions.StartPath + "/Example.cs", @"
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
            if (File.Exists(invokeOptions.StartPath + "/Example.cs"))
                File.Delete(invokeOptions.StartPath + "/Example.cs");
        }

        public static void AddBuiltinLogMethods()
        {

        }

        private static void EnsureLogFolders()
        {
            if(!Directory.Exists(invokeOptions.LogFolderPath))
            {
                Directory.CreateDirectory(invokeOptions.LogFolderPath);
            }

            if (!Directory.Exists(invokeOptions.LogFolderPath + "/success"))
            {
                Directory.CreateDirectory(invokeOptions.LogFolderPath + "/success");
            }

            if (!Directory.Exists(invokeOptions.LogFolderPath + "/error"))
            {
                Directory.CreateDirectory(invokeOptions.LogFolderPath + "/error");
            }
        }
        private static string CalculateCacheKey(MethodInfo methodInfo, MethodSettings methodSettings, object[]? inputParams, DynaUser? dynaUser)
        {
            string paramKey = inputParams is null ? "" : $".{inputParams.SerializeO().GetHashCode()}";
            if (dynaUser is null && methodSettings.CachePolicy.CacheLevel == CacheLevel.PerUser)
                throw new Exception($"CacheLevel for a method [{methodInfo.GetFullName()}] is PerUser so DynaUser can not be null.");
            string levelName = methodSettings.CachePolicy.CacheLevel == CacheLevel.PerUser ? $".{dynaUser.UserName}" : "";
            return $"{methodInfo.GetFullName()}{levelName}{paramKey}";
        }

    }
}