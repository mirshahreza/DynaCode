using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace AppEnd
{
    public static class Utils
    {
        public static string SerializeObjectsAsJson(this object[]? inputParams, MethodInfo methodInfo)
        {
            if (inputParams is null) return "{}";
            ParameterInfo[] parameterInfos = methodInfo.GetParameters();
            Dictionary<string, object> keyValuePairs = new();            
            int i = 0;
            foreach (object o in inputParams)
            {
                keyValuePairs[parameterInfos[i].Name] = o;
                i++;
            }
            return keyValuePairs.SerializeO();
        }

        public static string SerializeO(this object? o, bool indented = true)
        {
            if (o is null) return "";
            return JsonSerializer.Serialize(o, options: new()
            {
                IncludeFields = true,
                WriteIndented = indented,
                IgnoreReadOnlyProperties = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            });
        }

        public static string CreateLogContent(MethodInfo methodInfo, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string clientSection = "ClientInfo:" + Environment.NewLine + clientInfo + Environment.NewLine + Environment.NewLine;
            string inputsSection = "MethodInput:" + Environment.NewLine
                + inputParams.SerializeObjectsAsJson(methodInfo)
                + Environment.NewLine + Environment.NewLine;
            string outputsSection = "MethodOutput:" + Environment.NewLine + codeInvokeResult.Result.SerializeO() + Environment.NewLine + Environment.NewLine;

            return clientSection + inputsSection + outputsSection;
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

        public static IEnumerable<string> GetFiles(string path, string searchPattern)
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

        public static object ToOrigType(this JsonElement s, ParameterInfo parameterInfo)
        {
            if (parameterInfo.ParameterType == typeof(int)) return int.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int16)) return Int16.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int32)) return Int32.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Int64)) return Int64.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(bool)) return bool.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(DateTime)) return DateTime.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Guid)) return Guid.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(float)) return float.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(Decimal)) return Decimal.Parse(s.ToString());
            if (parameterInfo.ParameterType == typeof(string)) return s.ToString();
            if (parameterInfo.ParameterType == typeof(byte[])) return Encoding.UTF8.GetBytes(s.ToString());
            return s;
        }

    }
}
