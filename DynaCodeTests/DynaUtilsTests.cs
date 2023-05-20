using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using AppEnd;

namespace DynaCodeTests
{

    [TestClass()]
    public class DynaUtilsTests
    {
        [TestMethod()]
        public void CodeInvodeSimpleArraySandboxNSSandboxTSandboxM1()
        {
            DynaCode.Init("DynaCodeRoot");
            int r = (int)DynaCode.CodeInvode("SandboxNS", "SandboxT", "SandboxM1", new object[] { 4, 6 });
            Assert.AreEqual(r, 10);
        }

        [TestMethod()]
        public void CodeInvodeSubFolderTest()
        {
            DynaCode.Init("DynaCodeRoot");
            int r = (int)DynaCode.CodeInvode("SandboxNS", "SandboxTNew", "SandboxM1", new object[] { 7, 6 });
            Assert.AreEqual(r, 13);
        }

        [TestMethod()]
        public void CodeInvodeJsonElementAsParamsTest()
        {
            DynaCode.Init("DynaCodeRoot");

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            int r1 = (int)DynaCode.CodeInvode("SandboxNS", "SandboxT", "SandboxM2", root);
            Assert.AreEqual(r1, 5);
        }

        [TestMethod()]
        public void CodeInvodeMethodFullPathTest()
        {
            DynaCode.Init("DynaCodeRoot");

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            int r1 = (int)DynaCode.CodeInvode("SandboxNS.SandboxT.SandboxM2", root);
            Assert.AreEqual(r1, 5);
        }


        [TestMethod()]
        public void RefreshTest()
        {
            DynaCode.Init("DynaCodeRoot", false, true);

            try
            {
                int r1 = (int)DynaCode.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
            }

            DynaCode.AddExampleCode();
            DynaCode.Refresh();

            int r2 = (int)DynaCode.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            Assert.AreEqual(r2, 10);

            DynaCode.RemoveExampleCode();
            DynaCode.Refresh();

            try
            {
                int r3 = (int)DynaCode.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
            }

        }
    }
}