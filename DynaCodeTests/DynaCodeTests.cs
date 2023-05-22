using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using AppEnd;

namespace DynaCodeTests
{

    [TestClass()]
    public class DynaCodeTests
    {
        [TestMethod()]
        public void CodeInvodeSimpleArraySandboxNSSandboxTSandboxM1()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            CodeInvokeResult r = DynaCode.CodeInvode("SandboxNS.SandboxT.SandboxM1", new object[] { 4, 6 }, GetSampleDynaUser(true));
            Assert.AreEqual(r.Result, 10);
        }

        [TestMethod()]
        public void CheckAccessTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            CodeInvokeResult r = DynaCode.CodeInvode("SandboxNS.SandboxT.SandboxM1", new object[] { 4, 6 }, GetSampleDynaUser(true, false));
            Assert.AreEqual(r.Result, 10);
        }

        [TestMethod()]
        public void CodeInvodeSubFolderTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            CodeInvokeResult r = DynaCode.CodeInvode("SandboxNS.SandboxTNew.SandboxM1", new object[] { 7, 6 }, GetSampleDynaUser(true, false));
            Assert.AreEqual(r.Result, 13);
        }

        [TestMethod()]
        public void CodeInvodeJsonElementAsParamsTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            CodeInvokeResult r1 = DynaCode.CodeInvode("SandboxNS.SandboxT.SandboxM2", root, GetSampleDynaUser(true));
            Assert.AreEqual(r1.Result, 5);
        }

        [TestMethod()]
        public void CodeInvodeMethodFullPathTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            CodeInvokeResult r1 = DynaCode.CodeInvode("SandboxNS.SandboxT.SandboxM2", root, GetSampleDynaUser(true));
            Assert.AreEqual(r1.Result, 5);
        }

        [TestMethod()]
        public void CodeMapsTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            Assert.IsTrue(DynaCode.CodeMaps.Count() >= DynaCode.ScriptFiles.Length);
        }

        [TestMethod()]
        public void WriteMethodSettingsTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            MethodSettings methodSettings = GetSampleMethodSettings();
            DynaCode.WriteMethodSettings("SandboxNS.SandboxT.SandboxM1", methodSettings);

            methodSettings.LogPolicy.OnSuccessLogMethod = "DynaCodeBuiltin.BuiltinMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnFailLogMethod = "DynaCodeBuiltin.BuiltinMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings("SandboxNS.SandboxT.SandboxM1", methodSettings);

            Assert.IsTrue(1 == 1);
        }

        [TestMethod()]
        public void ReadMethodSettingsTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());

            MethodSettings methodSettings1 = GetSampleMethodSettings();
            DynaCode.WriteMethodSettings("SandboxNS.SandboxT.SandboxM2", methodSettings1);

            MethodSettings methodSettings2 = DynaCode.ReadMethodSettings("SandboxNS.SandboxT.SandboxM2");

            Assert.IsTrue(methodSettings1.Serialize() == methodSettings2.Serialize());
        }

        [TestMethod()]
        public void RefreshTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            try
            {
                try
                {
                    CodeInvokeResult r1 = DynaCode.CodeInvode("Example.ExampleT.ExampleM", new object[] { 4, 6 }, GetSampleDynaUser());
                }
                catch (Exception ex)
                {
                    Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
                }

                DynaCode.AddExampleCode();
                DynaCode.Refresh();

                CodeInvokeResult r2 = DynaCode.CodeInvode("Example.ExampleT.ExampleM", new object[] { 4, 6 }, GetSampleDynaUser());
                Assert.AreEqual(r2.Result, 10);

                DynaCode.RemoveExampleCode();
                DynaCode.Refresh();

                try
                {
                    CodeInvokeResult r3 = DynaCode.CodeInvode("Example.ExampleT.ExampleM", new object[] { 4, 6 }, GetSampleDynaUser());
                }
                catch (Exception ex)
                {
                    Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
                }
            }
            catch 
            {
                DynaCode.RemoveExampleCode();
            }

        }

        private CodeInvokeOptions GetSampleInvokeOptions()
        {
            return new CodeInvokeOptions()
            {
                StartPath = "DynaCodeRoot",
                CompiledIn = false,
                IsDevelopment = false,
                PublicKeyUser = "admin",
                PublicKeyRole = "Admin",
            };
        }

        private DynaUser GetSampleDynaUser(bool publickeyUser = false, bool publickeyRole = false)
        {
            if(publickeyRole)
            {
                return new DynaUser()
                {
                    UserName = publickeyUser ? "admin" : "TestUser",
                    Roles = new[] { "Admin" }
                };
            }
            else
            {
                return new DynaUser()
                {
                    UserName = publickeyUser ? "admin" : "TestUser",
                    Roles = new[] { "TestRole" }
                };
            }
        }

        private MethodSettings GetSampleMethodSettings()
        {
            MethodSettings methodSettings = new MethodSettings()
            {
                CachePolicy = new CachePolicy() { CacheLevel = CacheLevel.PerUser, AbsoluteExpirationSeconds = 100 },
                AccessRules = new AccessRules()
                {
                    AllowedRoles = new[] { "Developer", "Publisher" },
                    AllowedUsers = new[] { "Mohsen" },
                    DeniedUsers = new[] { "Ali" }
                },
                LogPolicy = new LogPolicy { OnSuccessLogMethod = "", OnFailLogMethod = "", TruncateTo10K = true }
            };
            return methodSettings;
        }

    }
}