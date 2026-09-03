using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace QSLink
{
    internal class ToolDef
    {
        public string Key;           // 命令行 key，如 "kptools"
        public string ExeName;       // 释放到磁盘的文件名，如 "kptools.exe"
        public string[] ResourceNames; // 可能的嵌入资源名（按优先级）
        public bool NeedsMsysDlls;   // 是否需要释放 msys-2.0.dll / msys-z.dll
    }

    internal static class Program
    {
        // 版本号（SLink.exe version 时显示）
        private const string AppVersion = "1.0.0";

        // 工作目录：用于释放 exe / dll / kpimg
        private static readonly string WorkDir = Path.Combine(
            Path.GetTempPath(),
            "QSLink_" + GetShortHash(AppContext.BaseDirectory));

        // 支持的子工具
        private static readonly ToolDef[] Tools = new[]
        {
            new ToolDef
            {
                Key = "kptools",
                ExeName = "kptools.exe",
                ResourceNames = new[] { "QSLink.programs.kptools.exe" },
                NeedsMsysDlls = true,
            },
            new ToolDef
            {
                Key = "githubdl",
                ExeName = "githubdl.exe",
                ResourceNames = new[] { "QSLink.programs.githubdl.exe" },
                NeedsMsysDlls = false,
            },
            new ToolDef
            {
                Key = "yq",
                ExeName = "yq.exe",
                ResourceNames = new[] { "QSLink.programs.yq.exe" },
                NeedsMsysDlls = false,
            },
            new ToolDef
            {
                Key = "magiskboot",
                ExeName = "magiskboot.exe",
                ResourceNames = new[] { "QSLink.programs.magiskboot.exe" },
                NeedsMsysDlls = false,
            },
        };

        private static int Main(string[] args)
        {
            // 0) 顶层保护：任何未处理异常都打印出来，避免一闪而过
            try
            {
                return RealMain(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[QSLink] 发生错误: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                PauseBeforeExit("发生错误");
                return 99;
            }
        }

        private static int RealMain(string[] args)
        {
            // ---- 没有参数 → 打印用法并等待（防止双击闪退）----
            if (args.Length == 0)
            {
                PrintUsage();
                PauseBeforeExit("无参数");
                return 0;
            }

            // ---- version ----
            if (IsMatch(args[0], "version", "v"))
            {
                Console.WriteLine("QSLink " + AppVersion);
                return 0;
            }

            // ---- help ----
            if (IsMatch(args[0], "help", "h", "--help", "-h"))
            {
                PrintUsage();
                return 0;
            }

            // ---- 查找子工具 ----
            var tool = FindTool(args[0]);
            if (tool == null)
            {
                Console.Error.WriteLine("未知命令: " + args[0]);
                PrintUsage();
                PauseBeforeExit("未知命令");
                return 1;
            }

            // ---- 运行 kptools（带默认 kpimg 逻辑）----
            if (tool.Key == "kptools")
            {
                return RunKptools(Slice(args, 1));
            }

            // ---- 通用运行（其他工具，参数原样透传）----
            EnsureExtracted(tool);
            return RunToolInConsole(tool, Slice(args, 1));
        }

        private static int RunKptools(string[] rawArgs)
        {
            var tool = Tools[0];
            EnsureExtracted(tool);

            var args = new ListStr(rawArgs);

            // 帮助 / 版本：直接透传，不追加 --kpimg
            if (args.Any(a => IsMatch(a, "--help", "-h", "help", "/?")) ||
                args.Any(a => IsMatch(a, "--version", "-v", "version")))
            {
                return RunToolInConsole(tool, args.Items);
            }

            // 找到 -k / --kpimg 的位置
            int idx = IndexOfAny(args, "-k", "--kpimg");
            if (idx >= 0 && idx + 1 < args.Count)
            {
                string supplied = args[idx + 1];
                if (!File.Exists(supplied))
                {
                    // 路径不存在 → 使用内置 kpimg
                    args[idx + 1] = ExtractKpimg();
                }
                // 否则使用用户提供的路径（不覆盖）
            }
            else
            {
                // 完全没有 -k / --kpimg → 自动追加内置 kpimg
                args.Add("--kpimg");
                args.Add(ExtractKpimg());
            }

            return RunToolInConsole(tool, args.Items);
        }

        private static int RunToolInConsole(ToolDef tool, string[] args)
        {
            string exePath = Path.Combine(WorkDir, tool.ExeName);

            // 确保 dll 都在同一目录（msys 加载依赖）
            if (tool.NeedsMsysDlls)
            {
                ExtractAllDlls();
            }

            // 双击/无控制台环境：先给自己绑一个控制台，这样后面打印能显示
            if (!HasConsole())
            {
                AllocConsoleForMyself();
            }

            // ★ 统一走"文件重定向 + 不弹窗"，彻底杜绝新窗口/闪退
            return RunWithFileRedirection(exePath, args);
        }

        // 统一路径：CreateNoWindow=true（不弹子窗口）+ 输出重定向到临时文件，
        // 进程结束后读回并打印到父进程控制台。这是兼顾"无新窗口"和
        // "msys2 程序能正常输出"的最可靠方式。
        private static int RunWithFileRedirection(string exePath, string[] args)
        {
            string outFile = Path.Combine(WorkDir, "stdout.txt");
            string errFile = Path.Combine(WorkDir, "stderr.txt");

            // 先清空旧文件
            SafeDelete(outFile);
            SafeDelete(errFile);

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = JoinArgs(args),
                UseShellExecute = false,
                CreateNoWindow = true,       // ★ 关键：不弹子窗口 → 不闪退
                WorkingDirectory = WorkDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                using (var p = Process.Start(psi))
                {
                    if (p == null) throw new InvalidOperationException("Process.Start 返回 null");

                    // 用后台线程持续把管道数据写入文件（同步 ReadToEnd 会死锁，
                    // 因为子进程可能同时写 stdout/stderr 且缓冲区满）
                    var tOut = System.Threading.Tasks.Task.Run(() => WriteToFile(p.StandardOutput, outFile));
                    var tErr = System.Threading.Tasks.Task.Run(() => WriteToFile(p.StandardError, errFile));

                    // 等待进程退出 + 两个写文件任务都完成
                    p.WaitForExit();
                    System.Threading.Tasks.Task.WaitAll(tOut, tErr);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[QSLink] 启动子进程失败: " + ex.Message);
                Console.Error.WriteLine("请确认已释放文件: " + exePath);
                PauseBeforeExit("启动失败");
                return 90;
            }

            // 把文件内容打到我们的控制台
            WriteFileToConsole(outFile, toError: false);
            WriteFileToConsole(errFile, toError: true);

            // 双击场景下防止窗口一闪而过
            PauseBeforeExit("执行结束");
            return 0;
        }

        // 持续读取 reader 并追加写入文件（在后台线程跑，避免死锁）
        private static void WriteToFile(StreamReader reader, string path)
        {
            try
            {
                using (reader)
                using (var writer = new StreamWriter(path, false, Encoding.UTF8))
                {
                    // 逐行拷贝，既避免一次性 ReadToEnd 死锁，也能及时落盘
                    char[] buffer = new char[4096];
                    while (!reader.EndOfStream)
                    {
                        int n = reader.Read(buffer, 0, buffer.Length);
                        if (n <= 0) break;
                        writer.Write(buffer, 0, n);
                        writer.Flush(); // 实时落盘，便于调试
                    }
                }
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(path + ".err", ex.ToString()); } catch { }
            }
        }

        // 把文件内容输出到控制台
        private static void WriteFileToConsole(string path, bool toError)
        {
            if (!File.Exists(path)) return;
            try
            {
                string s = File.ReadAllText(path, Encoding.UTF8);
                if (s.Length > 0)
                {
                    if (toError) Console.Error.Write(s);
                    else Console.Write(s);
                }
            }
            catch
            {
                // 忽略读取失败
            }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ============================================================
        //  控制台检测 / 绑定（仅 Windows 有意义）
        // ============================================================
        private static bool HasConsole()
        {
            try
            {
                // 若能读取/写入控制台，说明已有控制台
                var _ = Console.WindowHeight; // 无控制台时会抛异常
                Console.Write("");            // 触发绑定
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AllocConsoleForMyself()
        {
            try
            {
                // Win32: 给自己分配一个控制台
                AllocConsole();
            }
            catch
            {
                // 失败也无妨，尽量继续
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        // ============================================================
        //  资源释放
        // ============================================================
        private static void EnsureExtracted(ToolDef tool)
        {
            Directory.CreateDirectory(WorkDir);

            // 主 exe
            ExtractResource(tool.ResourceNames, tool.ExeName);

            // msys dll（如果需要）
            if (tool.NeedsMsysDlls)
            {
                ExtractAllDlls();
            }
        }

        private static void ExtractAllDlls()
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                // 只处理 programs 下的 .dll
                if (!name.StartsWith("QSLink.programs.", StringComparison.OrdinalIgnoreCase)) continue;
                string fileName = name.Substring("QSLink.programs.".Length);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;

                string dest = Path.Combine(WorkDir, fileName);
                if (File.Exists(dest)) continue;

                using var src = asm.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException("缺少嵌入资源: " + name);
                using var dst = File.Create(dest);
                src.CopyTo(dst);
            }
        }

        private static string ExtractKpimg()
        {
            const string resourceName = "QSLink.programs.kpimg";
            string outPath = Path.Combine(WorkDir, "kpimg"); // 无后缀
            Directory.CreateDirectory(WorkDir);

            if (!File.Exists(outPath))
            {
                var asm = Assembly.GetExecutingAssembly();
                using var src = asm.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException(
                        "缺少内置 kpimg 资源: " + resourceName +
                        "\n可用资源:\n  " + string.Join("\n  ", asm.GetManifestResourceNames()));
                using var dst = File.Create(outPath);
                src.CopyTo(dst);
            }
            return outPath;
        }

        private static void ExtractResource(string[] possibleNames, string destFileName)
        {
            var asm = Assembly.GetExecutingAssembly();
            string outPath = Path.Combine(WorkDir, destFileName);

            if (File.Exists(outPath))
            {
                // 已存在则跳过（可改为按版本覆盖）
                return;
            }

            string chosen = null;
            foreach (var n in possibleNames)
            {
                if (asm.GetManifestResourceStream(n) != null)
                {
                    chosen = n;
                    break;
                }
            }

            if (chosen == null)
            {
                throw new InvalidOperationException(
                    "找不到嵌入资源（尝试过）:\n  " + string.Join("\n  ", possibleNames) +
                    "\n可用资源:\n  " + string.Join("\n  ", asm.GetManifestResourceNames()));
            }

            using var src = asm.GetManifestResourceStream(chosen);
            using var dst = File.Create(outPath);
            src.CopyTo(dst);
        }

        // ============================================================
        //  参数工具
        // ============================================================
        private static ToolDef FindTool(string key)
        {
            foreach (var t in Tools)
            {
                if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        private static int IndexOfAny(ListStr list, params string[] candidates)
        {
            for (int i = 0; i < list.Count; i++)
            {
                foreach (var c in candidates)
                {
                    if (string.Equals(list[i], c, StringComparison.OrdinalIgnoreCase)) return i;
                }
            }
            return -1;
        }

        private static string JoinArgs(string[] args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (NeedsQuote(a))
                    sb.Append("\"" + a.Replace("\"", "\\\"") + "\"");
                else
                    sb.Append(a);
            }
            return sb.ToString();
        }

        private static bool NeedsQuote(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            if (s.Contains(' ') || s.Contains('\t') || s.Contains('"')) return true;
            return false;
        }

        private static string[] Slice(string[] args, int start)
        {
            if (start >= args.Length) return new string[0];
            var r = new string[args.Length - start];
            for (int i = start; i < args.Length; i++) r[i - start] = args[i];
            return r;
        }

        private static bool IsMatch(string value, params string[] candidates)
        {
            foreach (var c in candidates)
            {
                if (string.Equals(value, c, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 简易 List<string> 包装
        private class ListStr
        {
            private readonly System.Collections.Generic.List<string> _inner
                = new System.Collections.Generic.List<string>();

            // ★ 修复 CS1729：支持 new ListStr(string[])（第 141 行）
            public ListStr() { }

            public ListStr(System.Collections.Generic.IEnumerable<string> items)
            {
                foreach (var s in items) _inner.Add(s);
            }

            public int Count => _inner.Count;
            public string this[int i] { get => _inner[i]; set => _inner[i] = value; }
            public void Add(string s) => _inner.Add(s);
            public bool Any(System.Func<string, bool> pred)
            {
                foreach (var x in _inner) if (pred(x)) return true;
                return false;
            }
            public string[] Items => _inner.ToArray();
        }

        private static string GetShortHash(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < 8; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("QSLink " + AppVersion);
            Console.WriteLine();
            Console.WriteLine("用法:");
            Console.WriteLine("  QSLink.exe version           显示版本号");
            Console.WriteLine("  QSLink.exe [Tools] [参数]     运行内嵌的工具");
            Console.WriteLine();
            Console.WriteLine("注意: 建议在 cmd / PowerShell 中运行，以便正确显示控制台输出。");
        }

        // 在可能闪退的退出点前暂停（双击场景）
        private static void PauseBeforeExit(string reason)
        {
            // 仅在确实没有控制台输入能力时暂停
            try
            {
                Console.WriteLine();
                Console.WriteLine("[QSLink] " + reason + "，按任意键退出...");
                Console.ReadKey(true);
            }
            catch
            {
                // 无控制台则忽略
            }
        }
    }
}
