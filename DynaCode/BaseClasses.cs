using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppEnd
{
    public class MethodSettings
    {
        public AccessRules AccessRules = new() { AllowedRoles = [], AllowedUsers = [], DeniedUsers = [] };
        public CachePolicy CachePolicy = new() { CacheLevel = CacheLevel.None };
        public LogPolicy LogPolicy = new() { OnErrorLogMethod = "", OnSuccessLogMethod = "" };
        public string Serialize()
        {
            return JsonSerializer.Serialize(this, options: new()
            {
                IncludeFields = true,
                WriteIndented = false,
                IgnoreReadOnlyProperties = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
            });
        }
    }
    public class LogPolicy
    {
        public string OnSuccessLogMethod = "";
        public string OnErrorLogMethod = "";
    }
    public class CachePolicy
    {
        public CacheLevel CacheLevel;
        public int AbsoluteExpirationSeconds;
    }
    public class AccessRules
    {
        public string[] AllowedRoles { set; get; }
        public string[] AllowedUsers { set; get; }
        public string[] DeniedUsers { set; get; }
    }

    public class CodeMap
    {
        public string MethodFullName { get; init; }
        public string FilePath { get; init; }
    }

    public class SourceCode
    {
        public string FilePath { get; init; }
        public string RawCode { get; init; }
    }

    public class CodeInvokeResult
    {
        public long Duration { get; init; }
        public object? Result { get; init; }
        public bool IsSucceeded { get; init; }
        public bool? FromCache { get; init; }
    }

    public class CodeInvokeOptions
    {
        public string StartPath { get; init; }
        public string LogFolderPath { get; init; } = "log";
        public bool CompiledIn { get; init; } = false;
        public bool IsDevelopment { get; init; } = false;
        public string PublicKeyUser { get; init; } = "";
        public string PublicKeyRole { get; init; } = "";
        public string[]? PublicMethods { get; init; } 
        public string AlternativeMethodFullName { get; init; } = "";
        public string ReferencesPath { get; init; } = "";
    }

    public class DynaClass
    {
        public string? Namespace { set; get; }
        public string Name { set; get; }
        public List<DynaMethod> DynaMethods { set; get; }
    }

    public class DynaMethod
    {
        public string Name { set; get; } = "";
        public MethodSettings MethodSettings { set; get; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CacheLevel
    {
        None,
        PerUser,
        AllUsers
    }


}
