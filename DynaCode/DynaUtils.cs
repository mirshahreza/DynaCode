using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Text.Json.JsonElement;

namespace DynaCode
{
    public static class DynaUtils
    {
        public static object? CodeInvode(string? nameSpaceName, string typeName, string methodName, JsonElement inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(nameSpaceName, typeName, methodName);
            return CodeInvode(nameSpaceName, typeName, methodName, ExtractParams(methodInfo, inputParams));
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
        public static object ToOrigType(this JsonElement s, ParameterInfo parameterInfo)
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

        public static object? CodeInvode(string? nameSpaceName, string typeName, string methodName, object[] inputParams)
        {
            MethodInfo methodInfo = GetMethodInfo(nameSpaceName, typeName, methodName);
            return methodInfo.Invoke(null, inputParams);
        }

        public static object? CodeInvode(MethodInfo methodInfo, object[] inputParams)
        {
            return methodInfo.Invoke(null, inputParams);
        }

        public static MethodInfo GetMethodInfo(string? nameSpaceName, string typeName, string methodName)
        {
            if (typeName.Trim() == "") throw new Exception($"The TypeName can not be empty");
            string tn = nameSpaceName is null || nameSpaceName == "" ? typeName : nameSpaceName + "." + typeName;
            MethodInfo? methodInfo = GetType(tn).GetMethod(methodName);
            if (methodInfo == null) throw new Exception($"Requested method [{methodName}] does not exist.");
            return methodInfo;
        }

        public static Type GetType(string typeFullName)
        {
            string tName = typeFullName;
            string nsName = "";
            Type? dynamicType;
            if (typeFullName.Contains("."))
            {
                nsName = typeFullName.Split('.')[0];
                tName = typeFullName.Split(".")[1];
            }
            if (DynaCompiler.IsDevelopment || DynaCompiler.CodeIncluded)
            {
                dynamicType = Assembly.GetEntryAssembly()?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                if (dynamicType == null)
                {
                    dynamicType = DynaCompiler.DynaAsm?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                }
            }
            else
            {
                dynamicType = DynaCompiler.DynaAsm?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                if (dynamicType == null)
                {
                    dynamicType = Assembly.GetEntryAssembly()?.GetTypes().FirstOrDefault(i => i.Name == tName && (nsName == "" || i.Namespace == nsName));
                }
            }
            if (dynamicType == null) throw new Exception($"Requested type [ {typeFullName} ] does not exist.");
            return dynamicType;
        }

        internal static IEnumerable<string> GetFiles(string path, string searchPattern)
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

        public static string GetFullName(this MethodInfo methodInfo)
        {
            return methodInfo.DeclaringType.Namespace + "." + methodInfo.DeclaringType.Name + "." + methodInfo.Name;
        }

        public static void AddExampleCode()
        {
            File.WriteAllText(DynaCompiler.StartPath + "/Example.cs", @"
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
            if (File.Exists(DynaCompiler.StartPath + "/Example.cs"))
                File.Delete(DynaCompiler.StartPath + "/Example.cs");
        }


    }
}
