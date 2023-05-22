using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AppEnd
{
    public class MethodSettings
    {
        public CachePolicy CachePolicy = new CachePolicy() { CacheLevel = CacheLevel.None };
        public AccessRules AccessRules = new AccessRules() { AllowedUsers = new string[] { }, AllowedRoles = new string[] { }, DeniedUsers = new string[] { } };
        public LogPolicy LogPolicy = new LogPolicy() { OnFailLogMethod = "", OnSuccessLogMethod = "", TruncateTo10K = true };
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
        public string OnFailLogMethod = "";
        public bool TruncateTo10K = true;
    }
    public class CachePolicy
    {
        public CacheLevel CacheLevel;
        public int AbsoluteExpirationSeconds;
    }
    public class AccessRules
    {
        public string[] AllowedRoles { init; get; }
        public string[] AllowedUsers { init; get; }
        public string[] DeniedUsers { init; get; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CacheLevel
    {
        None,
        PerUser,
        AllUsers
    }

    public class DynaUser
    {
        public string UserName { init; get; }
        public string[] Roles { init; get; }
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

    }

}
