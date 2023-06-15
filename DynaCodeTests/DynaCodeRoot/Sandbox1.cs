using AppEnd;
using System.Text.Json;
using System.Text.Json.Nodes;
using System;

namespace SandboxNS
{
    public static class SandboxT
    {
        public static int SandboxM1(int a, int b)
        {
            return a + b;
        }

        public static int SandboxM2(int a, int b, string s)
        {
            return a + b + s.Length;
        }

        public static string SandboxM3(JsonElement p1, JsonNode jsonNode)
        {
            return p1.ToString() + " --- " + jsonNode.ToString();
        }

        public static string SandboxM4(DynaUser dynaUser)
        {
            return dynaUser.UserName;
        }

        public static string SandboxM5(int a, int b, string s)
        {
            return DateTime.Now.ToString();
        }

    }
}

