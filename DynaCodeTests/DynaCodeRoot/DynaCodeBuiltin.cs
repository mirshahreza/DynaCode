using AppEnd;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynaCodeBuiltin
{
    public static class BuiltinMethods
    {
        public static void FileSuccessLogger(MethodInfo methodInfo, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string content = CreateLogContent(methodInfo, actor, methodFullPath, inputParams, codeInvokeResult, clientInfo);
            DynaCode.LogImmed(content, "success", $"{methodFullPath}-{actor}-{codeInvokeResult.IsSucceeded}-");
        }

        public static void FileErrorLogger(MethodInfo methodInfo, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string content = CreateLogContent(methodInfo, actor, methodFullPath, inputParams, codeInvokeResult, clientInfo);
            DynaCode.LogImmed(content, "error", $"{methodFullPath}-{actor}-{codeInvokeResult.IsSucceeded}-");
        }

        private static string CreateLogContent(MethodInfo methodInfo, string actor, string methodFullPath, object[]? inputParams, CodeInvokeResult codeInvokeResult, string clientInfo)
        {
            string clientSection = "ClientInfo:" + Environment.NewLine + clientInfo + Environment.NewLine + Environment.NewLine;
            string inputsSection = "MethodInput:" + Environment.NewLine 
                + inputParams.SerializeObjects(methodInfo) 
                + Environment.NewLine + Environment.NewLine;
            string outputsSection = "MethodOutput:" + Environment.NewLine + codeInvokeResult.Result.SerializeO() + Environment.NewLine + Environment.NewLine;

            return clientSection + inputsSection + outputsSection;
        }


    }
}

