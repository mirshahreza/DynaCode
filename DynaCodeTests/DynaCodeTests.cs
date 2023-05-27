using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using AppEnd;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace DynaCodeTests
{

    [TestClass()]
    public class DynaCodeTests
    {
        [TestMethod()]
        public void CodeInvokeBySimpleArrayInputs()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM1";
            DynaCode.WriteMethodSettings(methodFullName, new MethodSettings());
 
            CodeInvokeResult r = DynaCode.InvokeMethodByParamsArrayInputs("SandboxNS.SandboxT.SandboxM1", new object[] { 4, 6 });
            Assert.AreEqual(r.Result, 10);
        }

        [TestMethod()]
        public void CodeInvokeByJsonElementInputs()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";
            DynaCode.WriteMethodSettings(methodFullName, new MethodSettings());

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            CodeInvokeResult r1 = DynaCode.InvodeMethodByJsonElementInputs(methodFullName, root);
            Assert.AreEqual(r1.Result, 5);
        }

        [TestMethod()]
        public void CodeInvokeInSubfolders()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxTNew.SandboxM1";
            DynaCode.WriteMethodSettings(methodFullName, new MethodSettings());

            CodeInvokeResult r = DynaCode.InvokeMethodByParamsArrayInputs(methodFullName, new object[] { 7, 6 });
            Assert.AreEqual(r.Result, 13);
        }

        [TestMethod()]
        public void CheckAccessForUsers()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM1";
            MethodSettings methodSettings = new MethodSettings();
            methodSettings.AccessRules.AllowedUsers = new string[] { "Ali" };
            DynaCode.WriteMethodSettings(methodFullName, methodSettings);

            DynaUser aliActor = new DynaUser() { UserName = "Ali" };
            DynaUser mohsenActor = new DynaUser() { UserName = "Mohsen" };

            CodeInvokeResult r1 = DynaCode.InvokeMethodByParamsArrayInputs(methodFullName, new object[] { 4, 6 }, aliActor);
            Assert.IsTrue(r1.IsSucceeded);

            CodeInvokeResult r2 = DynaCode.InvokeMethodByParamsArrayInputs(methodFullName, new object[] { 4, 6 }, mohsenActor);
            Assert.IsFalse(r2.IsSucceeded);
        }

        [TestMethod()]
        public void CodeInvokeLogTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";
            MethodSettings methodSettings = new MethodSettings();
            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodSettings);

            CodeInvokeResult r1 = DynaCode.InvokeMethodByParamsArrayInputs(methodFullName, new object[] { 4, 6, "Ali" });
            Assert.IsTrue(r1.IsSucceeded);
        }

        [TestMethod()]
        public void CodeInvokeDiferentInputTypes()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM3";

            MethodSettings methodSettings = new MethodSettings();
            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodSettings);

            string data = @"{""a"": 1,""b"": 1,""c"": ""ali"",""d"":{""p1"":321,""p2"":""this is a test""}}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            JsonNode jsonNode = JsonNode.Parse("{}");
            jsonNode["Test"] = "This is a value for Test property";

            CodeInvokeResult r1 = DynaCode.InvokeMethodByParamsArrayInputs(methodFullName, new object[] { root, jsonNode });
            Assert.IsTrue(r1.IsSucceeded);
        }

        [TestMethod()]
        public void MethodSettingsWrite()
        {
            DynaCode.Init(GetSampleInvokeOptions()); 
            string methodFullName = "SandboxNS.SandboxT.SandboxM1";

            MethodSettings methodSettings = GetSampleMethodSettings();
            DynaCode.WriteMethodSettings(methodFullName, methodSettings);

            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodSettings);

            Assert.IsTrue(1 == 1);
        }

        [TestMethod()]
        public void MethodSettingsRead()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";

            MethodSettings methodSettings1 = GetSampleMethodSettings();
            DynaCode.WriteMethodSettings(methodFullName, methodSettings1);

            MethodSettings methodSettings2 = DynaCode.ReadMethodSettings(methodFullName);

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
                    CodeInvokeResult r1 = DynaCode.InvokeMethodByParamsArrayInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
                }
                catch (Exception ex)
                {
                    Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
                }

                DynaCode.AddExampleCode();
                DynaCode.Refresh();

                CodeInvokeResult r2 = DynaCode.InvokeMethodByParamsArrayInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
                Assert.AreEqual(r2.Result, 10);

                DynaCode.RemoveExampleCode();
                DynaCode.Refresh();

                try
                {
                    CodeInvokeResult r3 = DynaCode.InvokeMethodByParamsArrayInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
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
        private MethodSettings GetSampleMethodSettings()
        {
            MethodSettings methodSettings = new MethodSettings()
            {
                CachePolicy = new CachePolicy() { CacheLevel = CacheLevel.None },
                AccessRules = new AccessRules()
                {
                    AllowedRoles = new[] { "Developer", "Publisher" },
                    AllowedUsers = new[] { "Ali,Mohsen" },
                    DeniedUsers = new string[] { }
                },
                LogPolicy = new LogPolicy { OnSuccessLogMethod = "", OnErrorLogMethod = "", TruncateTo10K = true }
            };
            return methodSettings;
        }

    }
}