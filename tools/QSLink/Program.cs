using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace QSLink
{
    /// <summary>

    /// </summary>
    class Program
    {
        // ===== 版本号（version 命令输出）=====
        const string AppVersion = "1.0.0";

        // ===== 工作目录：所有子程序释放到同一目录，便于配套 dll 查找 =====
        static readonly string WorkDir = Path.Combine(
            Path.GetTempPath(), "QSLink_" + GetHash(Assembly.GetExecutingAssembly().Location));
        static readonly ToolDef[] Tools =
        {
            new ToolDef("kptools", "QSLink.programs.a.exe", "kptools.exe",
                new[] { "QSLink.programs.msys-2.0.dll", "QSLink.programs.msys-z.dll" }),
        };

        static int Main(string[] args)
        {
            // 1) 无参数时：根据自己文件名判断
            string key = null;
            var allArgs = new List<string>();
            if (args.Length == 0)
            {
                string self = Path.GetFileNameWithoutExtension(
                    Environment.GetCommandLineArgs()[0]);
                key = self;
            }
            else
            {
                if (!args[0].StartsWith("-") && !args[0].StartsWith("/"))
                    key = args[0];
                allArgs = args.Skip(1).ToList();
            }

            if (key == null || key.Equals("version", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("QSLink " + AppVersion);
                return 0;
            }

            var tool = Tools.FirstOrDefault(t =>
                t.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (tool == null)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                return tool.Key.Equals("kptools", StringComparison.OrdinalIgnoreCase)
                    ? RunKptools(allArgs)
                    : RunGeneric(tool, allArgs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[QSLink] 启动失败: " + ex.Message);
                return 2;
            }
        }

        static int RunGeneric(ToolDef tool, List<string> args)
        {
            EnsureExtracted(tool);
            return RunProcess(Path.Combine(WorkDir, tool.ExeName), args);
        }

        static int RunKptools(List<string> args)
        {
            var tool = Tools.First(t => t.Key == "kptools");
            EnsureExtracted(tool);

            string builtinKpimg = ExtractKpimg();

            int idx = IndexOfAny(args, "-k", "--kpimg");
            if (idx >= 0 && idx + 1 < args.Count)
            {
                string suppliedPath = args[idx + 1];
                if (!File.Exists(suppliedPath))
                {
                    args[idx + 1] = builtinKpimg;
                }

            }
            else
            {

                args.Add("--kpimg");
                args.Add(builtinKpimg);
            }

            return RunProcess(Path.Combine(WorkDir, tool.ExeName), args);
        }

        static void EnsureExtracted(ToolDef tool)
        {
            Directory.CreateDirectory(WorkDir);

            // 主程序
            ExtractResource(tool.ResourceName, Path.Combine(WorkDir, tool.ExeName));

            // 配套 dll
            foreach (var sup in tool.SupportFiles)
            {
                string fileName = Path.GetFileName(sup);
                ExtractResource(sup, Path.Combine(WorkDir, fileName));
            }
        }

        static string ExtractKpimg()
        {
            string outPath = Path.Combine(WorkDir, "kpimg"); // 无后缀
            ExtractResource("QSLink.programs.kpimg", outPath);
            return outPath;
        }

        static void ExtractResource(string resourceName, string outPath)
        {
            // 已存在且完整则跳过（避免重复写盘）
            if (File.Exists(outPath) && new FileInfo(outPath).Length > 0)
                return;

            var asm = Assembly.GetExecutingAssembly();
            using var src = asm.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException(
                    $"嵌入资源未找到: {resourceName}\n请运行 ProbeResources 查看实际资源名。", resourceName);
            using var dst = File.Create(outPath);
            src.CopyTo(dst);
        }

        static int RunProcess(string exePath, List<string> args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = JoinArgs(args),
                UseShellExecute = false,
                WorkingDirectory = WorkDir, // dll 放在同目录，便于 kptools 加载
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("无法启动: " + exePath);
            p.WaitForExit();
            return p.ExitCode;
        }

        static string JoinArgs(List<string> args)
        {
            // 简单引号包裹，处理带空格路径
            return string.Join(" ", args.Select(a =>
                a.Contains(' ') || a.Contains('\t') ? "\"" + a + "\"" : a));
        }

        static int IndexOfAny(List<string> list, params string[] values)
        {
            for (int i = 0; i < list.Count; i++)
                for (int j = 0; j < values.Length; j++)
                    if (list[i].Equals(values[j], StringComparison.OrdinalIgnoreCase))
                        return i;
            return -1;
        }

        static string GetHash(string s)
        {
            using var h = System.Security.Cryptography.MD5.Create();
            var bytes = h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 8);
        }

        static void PrintUsage()
        {
            Console.WriteLine("QSLink " + AppVersion);
            Console.WriteLine("用法:");
            Console.WriteLine("  QSLink.exe version             显示版本号");
            Console.WriteLine("  QSLink.exe kptools [args...]   运行 kptools");
            Console.WriteLine("  QSLink.exe <tool> [args...]    运行其它子程序");
            Console.WriteLine();
            Console.WriteLine("kptools 参数说明:");
            Console.WriteLine("  -k / --kpimg <path>          指定 kpimg 文件");
            Console.WriteLine("  若未指定或文件不存在，自动使用内置 kpimg");
        }
    }

    class ToolDef
    {
        public string Key;             // 命令行 key
        public string ResourceName;    // 嵌入资源完整名
        public string ExeName;         // 磁盘文件名
        public string[] SupportFiles;  // 配套 dll

        public ToolDef(string key, string resourceName, string exeName, string[] supportFiles)
        {
            Key = key;
            ResourceName = resourceName;
            ExeName = exeName;
            SupportFiles = supportFiles;
        }
    }
}
