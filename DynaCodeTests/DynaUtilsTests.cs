using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace DynaCode.Tests
{
    [TestClass()]
    public class DynaUtilsTests
    {
        [TestMethod()]
        public void CodeInvodeSimpleArraySandboxNSSandboxTSandboxM1()
        {
            DynaCompiler.Init("DynaCodeRoot");
            int r = (int)DynaCompiler.CodeInvode("SandboxNS", "SandboxT", "SandboxM1", new object[] { 4, 6 });
            Assert.AreEqual(r, 10);
        }

        [TestMethod()]
        public void CodeInvodeSubFolderTest()
        {
            DynaCompiler.Init("DynaCodeRoot");
            int r = (int)DynaCompiler.CodeInvode("SandboxNS", "SandboxTNew", "SandboxM1", new object[] { 7, 6 });
            Assert.AreEqual(r, 13);
        }

        [TestMethod()]
        public void CodeInvodeJsonElementAsParamsTest()
        {
            DynaCompiler.Init("DynaCodeRoot");

            string data = @"{""a"": 1,""b"": 1,""s"": ""ali""}";
            using JsonDocument doc = JsonDocument.Parse(data);
            JsonElement root = doc.RootElement;

            int r1 = (int)DynaCompiler.CodeInvode("SandboxNS", "SandboxT", "SandboxM2", root);
            Assert.AreEqual(r1, 5);
        }


        [TestMethod()]
        public void RefreshTest()
        {
            DynaCompiler.Init("DynaCodeRoot", false, true);

            try
            {
                int r1 = (int)DynaCompiler.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
            }

            DynaCompiler.AddExampleCode();
            DynaCompiler.Refresh();

            int r2 = (int)DynaCompiler.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            Assert.AreEqual(r2, 10);

            DynaCompiler.RemoveExampleCode();
            DynaCompiler.Refresh();

            try
            {
                int r3 = (int)DynaCompiler.CodeInvode("Example", "ExampleT", "ExampleM", new object[] { 4, 6 });
            }
            catch (Exception ex)
            {
                Assert.AreEqual(ex.Message, "Requested type [ Example.ExampleT ] does not exist.");
            }

        }
    }
}