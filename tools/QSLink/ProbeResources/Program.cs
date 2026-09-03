// ProbeResources：独立小工具，打印 QSLink.exe 内所有嵌入资源的真实名称。
// 用法：
//   1) dotnet build ProbeResources.csproj -c Release
//   2) 把 bin\Release\net8.0\ProbeResources.exe 放到 已发布的 QSLink.exe 旁边
//   3) 或直接：dotnet run --project ProbeResources -- <path-to-QSLink.exe>
//
// 如果不传参，则探测当前目录下同名的 QSLink.exe / 自身 exe 所在目录。

using System;
using System.IO;
using System.Reflection;

namespace QSLink.Probe
{
    class Program
    {
        static int Main(string[] args)
        {
            string target = null;

            if (args.Length > 0 && File.Exists(args[0]))
            {
                target = args[0];
            }
            else
            {
                // 尝试找 QSLink.exe（与主程序同目录）
                string dir = AppContext.BaseDirectory;
                string candidate = Path.Combine(dir, "QSLink.exe");
                if (File.Exists(candidate)) target = candidate;
            }

            Assembly asm;
            if (target != null)
            {
                Console.WriteLine("加载: " + target);
                asm = Assembly.LoadFrom(target);
            }
            else
            {
                Console.WriteLine("未指定目标，探测自身程序集。");
                asm = Assembly.GetExecutingAssembly();
            }

            Console.WriteLine("程序集: " + asm.FullName);
            Console.WriteLine("嵌入资源列表 (共 " + asm.GetManifestResourceNames().Length + " 个):");
            Console.WriteLine("--------------------------------------------------");
            foreach (var name in asm.GetManifestResourceNames())
            {
                using var s = asm.GetManifestResourceStream(name);
                long len = s?.Length ?? 0;
                Console.WriteLine("  {0,-50} {1,12:N0} bytes", name, len);
            }
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("提示: 资源名规则通常为 {RootNamespace}.programs.{文件名}");

            return 0;
        }
    }
}
