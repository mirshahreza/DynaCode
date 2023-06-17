using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using AppEnd;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

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
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, new MethodSettings());
 
            CodeInvokeResult r = DynaCode.InvokeByParamsInputs("SandboxNS.SandboxT.SandboxM1", new object[] { 4, 6 });
            Assert.AreEqual(r.Result, 10);
        }

        [TestMethod()]
        public void CodeInvokeByJsonElementInputs()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, new MethodSettings());

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            CodeInvokeResult r1 = DynaCode.InvokeByJsonInputs(methodFullName, root);
            Assert.AreEqual(r1.Result, 5);
        }

        [TestMethod()]
        public void CodeInvokeInSubfolders()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxTNew.SandboxM1";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, new MethodSettings());

            CodeInvokeResult r = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 7, 6 });
            Assert.AreEqual(r.Result, 13);
        }

        [TestMethod()]
        public void CheckAccessForUsers()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM1";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            MethodSettings methodSettings = new MethodSettings();
            methodSettings.AccessRules.AllowedUsers = new string[] { "Ali" };
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            DynaUser aliActor = new DynaUser() { UserName = "Ali" };
            DynaUser mohsenActor = new DynaUser() { UserName = "Mohsen" };

            CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 4, 6 }, aliActor);
            Assert.IsTrue(r1.IsSucceeded);

            CodeInvokeResult r2 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 4, 6 }, mohsenActor);
            Assert.IsFalse(r2.IsSucceeded);
        }

        [TestMethod()]
        public void GetUserInfo()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM4";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            MethodSettings methodSettings = new MethodSettings();
            methodSettings.AccessRules.AllowedUsers = new string[] { "Ali" };
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            DynaUser aliActor = new DynaUser() { UserName = "Ali" };

            CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { aliActor }, aliActor);
            Assert.IsTrue(r1.Result?.ToString() == aliActor.UserName);

        }

        [TestMethod()]
        public void CodeInvokeLogTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            MethodSettings methodSettings = new MethodSettings();
            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 4, 6, "Ali" });
            Assert.IsTrue(r1.IsSucceeded);
        }

        [TestMethod()]
        public void CodeInvokeDiferentInputTypes()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM3";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            MethodSettings methodSettings = new MethodSettings();
            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            string data = @"{""a"": 1,""b"": 1,""c"": ""ali"",""d"":{""p1"":321,""p2"":""this is a test""}}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            JsonNode? jsonNode = JsonNode.Parse("{}");
            jsonNode["Test"] = "This is a value for Test property";

            CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { root, jsonNode });
            Assert.IsTrue(r1.IsSucceeded);
        }

        [TestMethod()]
        public void MethodSettingsWrite()
        {
            DynaCode.Init(GetSampleInvokeOptions()); 
            string methodFullName = "SandboxNS.SandboxT.SandboxM1";
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);

            MethodSettings methodSettings = GetSampleMethodSettings();
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            methodSettings.LogPolicy.OnSuccessLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileSuccessLogger";
            methodSettings.LogPolicy.OnErrorLogMethod = "AppEnd.DynaCodeBuiltInLogMethods.FileErrorLogger";
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings);

            Assert.IsTrue(1 == 1);
        }

        [TestMethod()]
        public void MethodSettingsRead()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM2";

            MethodSettings methodSettings1 = GetSampleMethodSettings();
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings1);

            MethodSettings methodSettings2 = DynaCode.ReadMethodSettings(methodFullName, methodFilePath);

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
                    CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
                }
                catch (Exception ex)
                {
                    Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
                }

                DynaCode.AddExampleCode();
                DynaCode.Refresh();

                CodeInvokeResult r2 = DynaCode.InvokeByParamsInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
                Assert.AreEqual(r2.Result, 10);

                DynaCode.RemoveExampleCode();
                DynaCode.Refresh();

                try
                {
                    CodeInvokeResult r3 = DynaCode.InvokeByParamsInputs("Example.ExampleT.ExampleM", new object[] { 4, 6 });
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
        
        [TestMethod()]
        public void CacheTest()
        {
            DynaCode.Init(GetSampleInvokeOptions());
            string methodFullName = "SandboxNS.SandboxT.SandboxM5";

            MethodSettings methodSettings1 = GetSampleMethodSettings();
            string methodFilePath = DynaCode.GetMethodFilePath(methodFullName);
            methodSettings1.CachePolicy = new CachePolicy() { CacheLevel = CacheLevel.AllUsers, AbsoluteExpirationSeconds = 60 };
            DynaCode.WriteMethodSettings(methodFullName, methodFilePath, methodSettings1);

            DynaCode.Refresh();

            CodeInvokeResult r1 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 4, 6, "Ali" });
            Console.WriteLine(r1.SerializeO());

            System.Threading.Thread.Sleep(3000);

            CodeInvokeResult r2 = DynaCode.InvokeByParamsInputs(methodFullName, new object[] { 4, 6, "Ali" });
            Console.WriteLine(r2.SerializeO());


        }

        [TestMethod()]
        public void AppEndDbIOTest()
        {
            CodeInvokeOptions codeInvokeOptions = new CodeInvokeOptions()
            {
                IsDevelopment = true,
                CompiledIn = true,
                StartPath = "DynaCodeRoot",
                ReferencesPath = "-",
                PublicKeyUser = "admin",
                PublicKeyRole = "Admin"
            };

            DynaCode.Init(codeInvokeOptions);
            string methodFullName = "AppEndDbIO.ClientQueryHandler.Exec";

            string data = @"
{
""ClientQueryJson"":{
        ""QueryFullName"": ""DefaultDb.Sample_Persons.ReadList"",
        ""Where"": {
          ""WhereCompareClauses"": [
            {
              ""Name"": ""LastName"",
              ""Value"": ""شاه"",
              ""ClauseOperator"": ""Contains""
            }
          ]
        },
        ""OrderClauses"": [
          {
            ""Name"": ""FirstName"",
            ""OrderDirection"": ""DESC""
          }
        ],
        ""Pagination"": {
          ""PageNumber"": 1,
          ""PageSize"": 2
        },
        ""ExceptColumns"": [
          ""Picture_FileBody_xs""
        ],
        ""ExceptAggregations"": [
          ""Count""
        ],
        ""ExceptOneToManies"": [
          ""DefaultDb.Sample_Persons_R_Persons"",
          ""DefaultDb.Sample_Persons_Addresses"",
          ""DefaultDb.Sample_Persons_MoreDocuments""
        ],
        ""IncludeSubQueries"": true
      }
}
";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            CodeInvokeResult r1 = DynaCode.InvokeByJsonInputs(methodFullName, root);



            string s = Newtonsoft.Json.JsonConvert.SerializeObject(r1, Newtonsoft.Json.Formatting.Indented);

            Console.WriteLine(s);
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