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
using Microsoft.CodeAnalysis.Text;

namespace AppEnd
{
    public static class DynaCode
    {
        private static CodeInvokeOptions invokeOptions = new();

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
                if (scriptFiles is null)
                {
                    scriptFiles = new DirectoryInfo(invokeOptions.StartPath).GetFilesRecursive("*.cs").ToArray();
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

        private static List<CodeMap>? codeMaps;
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

        public static void Init(CodeInvokeOptions? codeInvokeOptions = null)
        {
            if (codeInvokeOptions is not null) invokeOptions = codeInvokeOptions;
            Utils.EnsureLogFolders(invokeOptions);
            Refresh();
        }
        public static void Refresh()
        {
            string[] oldAsmFiles = Directory.GetFiles(".", "DynaAsm*");
            foreach (string oldAsmFile in oldAsmFiles)
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
            codeMaps = null;

            Assembly asm = DynaAsm;
        }

        public static CodeInvokeResult InvokeByJsonInputs(string methodFullPath, JsonElement? inputParams = null, AppEndUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return Invoke(methodInfo, ExtractParams(methodInfo, inputParams, dynaUser), dynaUser, clientInfo, ignoreCaching);
        }
        public static CodeInvokeResult InvokeByParamsInputs(string methodFullPath, object[]? inputParams = null, AppEndUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            MethodInfo methodInfo = GetMethodInfo(methodFullPath);
            return Invoke(methodInfo, inputParams, dynaUser, clientInfo, ignoreCaching);
        }

        private static CodeInvokeResult Invoke(MethodInfo methodInfo, object[]? inputParams = null, AppEndUser? dynaUser = null, string clientInfo = "", bool ignoreCaching = false)
        {
            string methodFullName = methodInfo.GetFullName();
            string methodFilePath = GetMethodFilePath(methodFullName);
            MethodSettings methodSettings = ReadMethodSettings(methodFullName, methodFilePath);

            if (methodSettings.CachePolicy != null && methodSettings.CachePolicy.CacheLevel == CacheLevel.PerUser && (dynaUser is null || dynaUser.UserName.Trim() == ""))
                throw new AppEndException($"CachePolicy.CacheLevelIsSetToPerUserButTheCurrentUserIsNull")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;

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
                    catch(Exception ex)
                    {
                        stopwatch.Stop();
                        Exception _ex = ex.InnerException is null ? ex : ex.InnerException;
                        codeInvokeResult = new()
                        {
                            Result = _ex,
                            FromCache = null,
                            IsSucceeded = false,
                            Duration = stopwatch.ElapsedMilliseconds
                        };

                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Exception _ex = ex.InnerException is null ? ex : ex.InnerException;
                codeInvokeResult = new()
                {
                    Result = _ex,
                    FromCache = null,
                    IsSucceeded = false,
                    Duration = stopwatch.ElapsedMilliseconds
                };
            }

            LogMethodInvoke(methodInfo, methodSettings, codeInvokeResult, inputParams, dynaUser, clientInfo);
            return codeInvokeResult;
        }

        public static string GetMethodFilePath(string methodFullName)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName == methodFullName);
            if (codeMap is null) throw new AppEndException("MethodDoesNotExist")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            return codeMap.FilePath;
        }
        public static string GetTypeFilePath(string typeFullName)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName.StartsWith(typeFullName));
            if (codeMap is null) throw new AppEndException("MethodDoesNotExist")
                    .AddParam("TypeFullName", typeFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            return codeMap.FilePath;
        }

        private static void CheckAccess(MethodInfo methodInfo, MethodSettings methodSettings, AppEndUser? actor)
        {
            if (actor is null) return;
            if (actor.UserName.ToLower() == invokeOptions.PublicKeyUser.ToLower()) return;
            if (actor.Roles.ContainsIgnoreCase(invokeOptions.PublicKeyRole)) return;
            if (actor.Roles.ToList().HasIntersect(methodSettings.AccessRules.AllowedRoles)) return;
            if (methodSettings.AccessRules.AllowedRoles.Contains("*")) return;
            if (methodSettings.AccessRules.AllowedUsers.ContainsIgnoreCase(actor.UserName)) return;
            if (methodSettings.AccessRules.AllowedUsers.Contains("*")) return;
            if (invokeOptions.PublicMethods is not null && invokeOptions.PublicMethods.ContainsIgnoreCase(methodInfo.GetFullName())) return;
            throw new AppEndException("AccessDenied")
                .AddParam("Method", methodInfo.GetFullName())
                .AddParam("Actor", actor.UserName)
                .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                ;
        }
        private static void LogMethodInvoke(MethodInfo methodInfo, MethodSettings methodSettings, CodeInvokeResult codeInvokeResult, object[]? inputParams, AppEndUser? dynaUser, string clientInfo = "")
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

        public static void DuplicateMethod(string methodFullName, string methodCopyName)
        {
            var parts = MethodPartsNames(methodFullName);
            string classFullName = methodFullName.Replace($".{parts.Item3}", "");
            string methodName = parts.Item3;
            string? filePath = GetMethodFilePath(methodFullName);
            if (filePath == null) throw new AppEndException("MethodFullNameDoesNotExist")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;

            string controllerBody = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(controllerBody);

            MethodDeclarationSyntax method =
                tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ToString() == methodName);
            string m = method.GetText().ToString();
            string mCopy = m.Replace($"{methodName}(", $"{methodCopyName}(");
            TextChange tc = new TextChange(method.Span, $"{m.Trim()}{Environment.NewLine}{Environment.NewLine}{mCopy}");
            controllerBody = tree.GetText().WithChanges(tc).ToString().RemoveWhitelines();

            File.WriteAllText(filePath, controllerBody);
            Refresh();
        }
        public static void CreateMethod(string typeFullName, string methodName)
        {
            string? filePath = GetTypeFilePath(typeFullName);
            if (filePath == null) throw new AppEndException("MethodFullNameDoesNotExist")
                    .AddParam("TypeFullName", typeFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;

            string controllerBody = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(controllerBody);

            string mBody = new AppEndMethod(methodName).MethodImplementation;

            MethodDeclarationSyntax method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Last();
            string m = method.GetText().ToString();
            TextChange tc = new TextChange(method.Span, $"{m.Trim()}{Environment.NewLine}{Environment.NewLine}{mBody}");
            controllerBody = tree.GetText().WithChanges(tc).ToString().RemoveWhitelines();

            File.WriteAllText(filePath, controllerBody);
            Refresh();
        }

        public static void RemoveMethod(string methodFullName)
        {
            var parts = MethodPartsNames(methodFullName);
            string classFullName = methodFullName.Replace($".{parts.Item3}", "");
            string methodName = parts.Item3;
            string? filePath = GetMethodFilePath(methodFullName);
            if (filePath == null) throw new AppEndException("MethodFullNameDoesNotExist")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            string controllerBody = File.ReadAllText(filePath);

            SyntaxTree tree = CSharpSyntaxTree.ParseText(controllerBody);
            MethodDeclarationSyntax method =
                tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .First(m => m.Identifier.ToString() == methodName);

            TextChange tc = new TextChange(method.Span, string.Empty);
            controllerBody = tree.GetText().WithChanges(tc).ToString().RemoveWhitelines();

            File.WriteAllText(filePath, controllerBody);
            Refresh();
        }

        public static void WriteMethodSettings(string methodFullName, MethodSettings methodSettings)
        {
            string methodFilePath = GetMethodFilePath(methodFullName);
            WriteMethodSettings(methodFullName, methodFilePath, methodSettings);
        }
        public static void WriteMethodSettings(string methodFullName, string methodFilePath, MethodSettings methodSettings)
        {
            string settingsFileName = GetSettingsFile(methodFilePath);
            string settingsRaw = File.Exists(settingsFileName) ? File.ReadAllText(settingsFileName) : "{}";
            JsonNode? jsonNode = JsonNode.Parse(settingsRaw);
            if (jsonNode is null) throw new AppEndException("DeserializeError")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("MethodSettings", methodSettings)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            jsonNode[methodFullName] = JsonNode.Parse(methodSettings.Serialize());
            File.WriteAllText(settingsFileName, jsonNode.ToString());
        }

        public static MethodSettings ReadMethodSettings(string methodFullName)
        {
            string methodFilePath = GetMethodFilePath(methodFullName);
            return ReadMethodSettings(methodFullName, methodFilePath);
        }
        public static MethodSettings ReadMethodSettings(string methodFullName, string methodFilePath)
        {
            string settingsFileName = GetSettingsFile(methodFilePath);
            string settingsRaw = File.Exists(settingsFileName) ? File.ReadAllText(settingsFileName) : "{}";
            try
            {
                var jsonNode = JsonNode.Parse(settingsRaw);
                if (jsonNode is null) throw new AppEndException("DeserializeError")
                        .AddParam("MethodFullName", methodFullName)
                        .AddParam("SettingsRaw", settingsRaw)
                        .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                        ;
                if (jsonNode[methodFullName] == null) return new();
                MethodSettings? methodSettings = jsonNode[methodFullName].Deserialize<MethodSettings>(options: new() { IncludeFields = true });
                if (methodSettings is null) return new();
                return methodSettings;
            }
            catch
            {
                throw new AppEndException($"SettingsAreNotValid")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("MethodFilePath", methodFilePath)
                    .AddParam("Settings", settingsRaw)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            }
        }

        public static List<DynaClass> GetDynaClasses()
        {
            var dynaClasses = new List<DynaClass>();
            foreach (var i in DynaAsm.GetTypes())
            {
                if (Utils.IsRealType(i.Name))
                {
                    List<DynaMethod> dynaMethods = new List<DynaMethod>();
                    foreach (var method in i.GetMethods())
                    {
                        if (Utils.IsRealMethod(method.Name))
                        {
                            dynaMethods.Add(new()
                            {
                                Name = method.Name,
                                MethodSettings = ReadMethodSettings($"{i.Namespace}.{i.Name}.{method.Name}")
                            });
                        }
                    }
                    DynaClass dynamicController = new() { Namespace = i.Namespace, Name = i.Name, DynaMethods = dynaMethods };
                    dynaClasses.Add(dynamicController);
                }
            }
            return dynaClasses;
        }


        private static string GetSettingsFile(string methodFilePath)
        {
            return methodFilePath.Replace(".cs", "") + ".settings.json";
        }

        public static void RemoveMethodSettings(string methodFullName)
        {
            CodeMap? codeMap = CodeMaps.FirstOrDefault(cm => cm.MethodFullName == methodFullName);
            if (codeMap is null) throw new AppEndException($"MethodDoesNotExist")
                    .AddParam("MethodFullName", methodFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
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
                throw new AppEndException("{error?.Id}: {error?.GetMessage()}")
                            .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                            ;
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

            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                AddReferencesFor(a, references);
            }

            AddReferencesFor(Assembly.GetExecutingAssembly(), references);
            AddReferencesFor(Assembly.GetEntryAssembly(), references);
            AddReferencesFor(Assembly.GetCallingAssembly(), references);

            AddReferencesFor(typeof(object).Assembly, references);
            AddReferencesFor(typeof(TypeConverter).Assembly, references);
            AddReferencesFor(Assembly.Load("netstandard, Version=2.1.0.0"), references);
            AddReferencesFor(typeof(System.Linq.Expressions.Expression).Assembly, references);
            AddReferencesFor(typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly, references);
            AddReferencesFor(typeof(Exception).Assembly, references);
            AddReferencesFor(typeof(AppEndException).Assembly, references);
            AddReferencesFor(typeof(ArgumentNullException).Assembly, references);

            if (Directory.Exists(invokeOptions.ReferencesPath))
            {
                foreach (string f in Directory.GetFiles(invokeOptions.ReferencesPath, "*.dll"))
                {
                    AddReferencesFor(Assembly.LoadFrom(f), references);
                }
            }

            return references;
        }
        private static void AddReferencesFor(Assembly? asm, List<MetadataReference> references)
        {
            if (asm is null || !File.Exists(asm.Location)) return;
            references.Add(MetadataReference.CreateFromFile(asm.Location));
            var rfs = asm.GetReferencedAssemblies();
            foreach(var a in rfs)
            {
                var asmF = Assembly.Load(a);
                if(asmF is null) continue;
                if(File.Exists( asmF.Location))
                {
                    references.Add(MetadataReference.CreateFromFile(asmF.Location));
                }
            }
            //references.AddRange(rfs.Select(a => MetadataReference.CreateFromFile(Assembly.Load(a).Location)));
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

        private static object[]? ExtractParams(MethodInfo methodInfo, JsonElement? jsonElement, AppEndUser? actor)
        {
            List<object> methodInputs = new List<object>();
            ParameterInfo[] methodParams = methodInfo.GetParameters();
            ObjectEnumerator? objects = jsonElement is null ? null : ((JsonElement)jsonElement).EnumerateObject();

            foreach (var paramInfo in methodParams)
            {
                if (paramInfo.Name.ToStringEmpty().ToLower() == "actor" && paramInfo.ParameterType == typeof(AppEndUser))
                {
                    methodInputs.Add(actor);
                }
                else
                {
                    if(objects is not null)
                    {
						IEnumerable<JsonProperty> l = ((ObjectEnumerator)objects).Where(i => string.Equals(i.Name, paramInfo.Name));
						if (l.Count() == 0) throw new AppEndException($"MethodCallMustContainsParameter")
								.AddParam("MethodFullName", methodInfo.GetFullName())
								.AddParam("ParameterName", paramInfo.Name.ToStringEmpty())
								.AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
								;
						JsonProperty p = l.First();
						methodInputs.Add(p.Value.ToOrigType(paramInfo));
					}
				}
            }
            return methodInputs.ToArray();
        }
        private static MethodInfo GetMethodInfo(string methodFullName)
        {
            var parts = MethodPartsNames(methodFullName);
            return  GetMethodInfo(parts.Item1, parts.Item2, parts.Item3);
        }

        public static Tuple<string?, string, string> MethodPartsNames(string methodFullPath)
        {
            if (methodFullPath.Trim() == "") throw new AppEndException("MethodFullPathCanNotBeEmpty")
                            .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                            ;
            string[] parts = methodFullPath.Trim().Split('.');
            if (parts.Length < 2 || parts.Length > 3) throw new AppEndException($"MethodMustContainsAtLeast2PartsSeparatedByDot")
                    .AddParam("MethodFullPath", methodFullPath)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            return parts.Length == 3 ? new(parts[0], parts[1], parts[2]) : new(null, parts[0], parts[1]);
        }
        private static MethodInfo GetMethodInfo(string? namespaceName, string className, string methodName)
        {
            if (className.Trim() == "") throw new AppEndException($"ClassNameCanNotBeEmpty")
                    .AddParam("MethodName", methodName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            string tn = namespaceName is null || namespaceName == "" ? className : namespaceName + "." + className;
            MethodInfo? methodInfo = GetType(tn).GetMethod(methodName);
            if (methodInfo == null) throw new AppEndException($"MethodDoesNotExist")
                    .AddParam("NamespaceName", namespaceName.ToStringEmpty())
                    .AddParam("ClassName", className)
                    .AddParam("MethodName", methodName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            return methodInfo;
        }
        private static Type GetType(string classFullName)
        {
            string tName = classFullName;
            string nsName = "";
            Type? dynamicType;
            if (classFullName.Contains("."))
            {
                nsName = classFullName.Split('.')[0];
                tName = classFullName.Split(".")[1];
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
            if (dynamicType == null) throw new AppEndException("TypeDoesNotExist")
                    .AddParam("ClassFullName", classFullName)
                    .AddParam("Site", $"{System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType?.Name}, {System.Reflection.MethodBase.GetCurrentMethod()?.Name}")
                    ;
            return dynamicType;
        }
        
        private static string CalculateCacheKey(MethodInfo methodInfo, MethodSettings methodSettings, object[]? inputParams, AppEndUser? dynaUser)
        {
            string paramKey = inputParams is null ? "" : $".{inputParams.ToJsonStringByBuiltIn().GetHashCode()}";
            string levelName = methodSettings.CachePolicy.CacheLevel == CacheLevel.PerUser ? $".{dynaUser?.UserName}" : "";
            return $"{methodInfo.GetFullName()}{levelName}{paramKey}";
        }


        public static Dictionary<string, object> GetAllAllowdAndDeniedActions(AppEndUser? actor)
        {
            if (actor == null) return new Dictionary<string, object> { { "AllowedActions", "".Split(',') } };

			List<DynaClass> dynaClasses = GetDynaClasses();
			List<string> alloweds = new List<string>();
			List<string> denieds = new List<string>();

			foreach (var dynaC in dynaClasses)
            {
                foreach(DynaMethod dynaM in dynaC.DynaMethods)
                {
					MethodSettings ms = dynaM.MethodSettings;
                    string mFullName = dynaC.Namespace + "." + dynaC.Name + "." + dynaM.Name;
                    if (
						invokeOptions.PublicMethods.ContainsIgnoreCase(mFullName) ||
						invokeOptions.PublicKeyUser.ToLower() == actor.UserName.ToLower() ||
						actor.Roles.ContainsIgnoreCase(invokeOptions.PublicKeyRole) ||
                        ms.AccessRules.AllowedUsers.ContainsIgnoreCase(actor.UserName) ||
                        ms.AccessRules.AllowedRoles.HasIntersect(actor.Roles)
                        )
                        alloweds.Add(mFullName);

					if (ms.AccessRules.DeniedUsers.ContainsIgnoreCase(actor.UserName))
						denieds.Add(mFullName);
				}
			}

            foreach(string s in denieds) if (alloweds.ContainsIgnoreCase(s)) alloweds.Remove(s);

            return new Dictionary<string, object> { { "AllowedActions", alloweds.ToArray() } };
		}

    }
}