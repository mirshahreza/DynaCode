using System.Reflection;

namespace AppEnd
{
	public static class Utils
    {
        public static string SerializeObjectsAsJson(this object[]? inputParams, MethodInfo methodInfo)
        {
            if (inputParams is null) return "{}";
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            Dictionary<string, object> keyValuePairs = [];            
            int i = 0;
            foreach (object o in inputParams)
            {
                keyValuePairs[parameterInfos[i].Name] = o;
                i++;
            }
            return keyValuePairs.ToJsonStringByBuiltIn();
        }

        public static string CreateLogContent(MethodInfo methodInfo, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
			string actorSection = $"Actor: {actor}{SV.NL2x}";
			string methodSection = $"Method: {methodFullPath}{SV.NL2x}";
			string clientSection = $"ClientInfo:{SV.NL}{clientInfo}{SV.NL2x}";
            string inputsSection = $"MethodInput:{SV.NL}{inputParams.SerializeObjectsAsJson(methodInfo)}{SV.NL2x}";
            string outputsSection = $"MethodOutput:{SV.NL}{codeInvokeResult.Result?.ToJsonStringByBuiltIn()}{SV.NL2x}";

            return actorSection + methodSection + clientSection + inputsSection + outputsSection;
        }

        public static void LogImmed(string content,string logFolder ,string subFolder = "", string filePreFix = "DynaLog-")
        {
            string fn = $"{filePreFix}{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}-{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}-{DateTime.Now.Millisecond}-{+(new Random()).Next(100)}.txt";
            File.WriteAllText(Path.Combine($"{logFolder}{(subFolder == "" ? "" : $"/{subFolder}")}", fn), content);
        }

        public static string GetFullName(this MethodInfo methodInfo)
        {
            if (methodInfo.DeclaringType is not null)
                return methodInfo.DeclaringType.Namespace + "." + methodInfo.DeclaringType.Name + "." + methodInfo.Name;
            else
                return methodInfo.Name;
        }

        public static void AddExampleCode(CodeInvokeOptions codeInvokeOptions)
        {
            File.WriteAllText(codeInvokeOptions.StartPath + "/Example.cs", @"
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
        public static void RemoveExampleCode(CodeInvokeOptions codeInvokeOptions)
        {
            if (File.Exists(codeInvokeOptions.StartPath + "/Example.cs"))
                File.Delete(codeInvokeOptions.StartPath + "/Example.cs");
        }

        public static void AddBuiltinLogMethods()
        {

        }

        public static void EnsureLogFolders(CodeInvokeOptions codeInvokeOptions)
        {
            if (!Directory.Exists(codeInvokeOptions.LogFolderPath))
            {
                Directory.CreateDirectory(codeInvokeOptions.LogFolderPath);
            }

            if (!Directory.Exists(codeInvokeOptions.LogFolderPath + "/success"))
            {
                Directory.CreateDirectory(codeInvokeOptions.LogFolderPath + "/success");
            }

            if (!Directory.Exists(codeInvokeOptions.LogFolderPath + "/error"))
            {
                Directory.CreateDirectory(codeInvokeOptions.LogFolderPath + "/error");
            }
        }


        public static bool IsRealType(string typeName)
        {
            CodeMap? codeMap = DynaCode.CodeMaps.FirstOrDefault(i => DynaCode.MethodPartsNames(i.MethodFullName).Item2 == typeName);
            return codeMap != null;
        }

        public static bool IsRealMethod(string methodName)
        {
            CodeMap? codeMap = DynaCode.CodeMaps.FirstOrDefault(i => DynaCode.MethodPartsNames(i.MethodFullName).Item3 == methodName);
            return codeMap != null;
        }

    }
}
