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
    }
}

