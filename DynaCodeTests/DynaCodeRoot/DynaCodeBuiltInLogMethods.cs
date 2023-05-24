using AppEnd;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppEnd
{
    public static class DynaCodeBuiltInLogMethods
    {
        public static void FileSuccessLogger(MethodInfo methodInfo, string logFolder, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string content = Utils.CreateLogContent(methodInfo, actor, methodFullPath, inputParams, codeInvokeResult, clientInfo);
            Utils.LogImmed(content, logFolder, "success", $"{methodFullPath}-{actor}-{codeInvokeResult.IsSucceeded}-");
        }

        public static void FileErrorLogger(MethodInfo methodInfo, string logFolder, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string content = Utils.CreateLogContent(methodInfo, actor, methodFullPath, inputParams, codeInvokeResult, clientInfo);
            Utils.LogImmed(content, logFolder, "error", $"{methodFullPath}-{actor}-{codeInvokeResult.IsSucceeded}-");
        }

    }
}

