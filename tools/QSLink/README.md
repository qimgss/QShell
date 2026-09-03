# QSLink —— 最终版说明

## 解决的核心问题："新建一个窗口然后闪退"

### 原因（重要，务必理解）
`kptools` 是 **msys2** 编译的控制台程序。之前各种方案都会触发 Windows 的窗口行为：

| 方案 | 结果 | 原因 |
|------|------|------|
| `UseShellExecute=true` | **弹新窗口** | msys2 子进程没有可继承的控制台时，Windows 为它新建一个 |
| `UseShellExecute=false` + 管道重定向 | **静默无输出** | msys2 检测到 stdout 是管道，直接退出 |
| 新窗口 + 命令结束 | **闪退** | 那个新窗口执行完立即关闭 |

### 最终采用的可靠方案（二选一，均已内置）

**统一策略：始终 `CreateNoWindow=true`（不弹子窗口）+ 输出重定向到【临时文件】**

1. kptools 在 `CreateNoWindow=true` 下**绝不弹窗** → 根治闪退；
2. 输出重定向到**文件**（不是管道）→ msys2 当作正常终端，正常打印；
3. 进程结束后，父进程（QSLink.exe）读回文件内容，打印到**自己的控制台**；
4. 双击/无控制台环境：先 `AllocConsole()` 给自己绑一个控制台，再输出，最后 `ReadKey` 防闪退。

这样做到：
- ✅ **绝对不弹新窗口**
- ✅ **不闪退**（有输出可见，结束前暂停）
- ✅ **msys2 程序能正常输出**（文件重定向，非管道）

## 目录结构

```
QSLink/
├── Program.cs                  ← 主程序（本文件，完整实现）
├── QSLink.csproj               ← 项目文件（排除 ProbeResources、精确嵌入资源）
├── publish.bat                 ← 一键发布
├── README.md                   ← 本文件
├── programs/                   ← ★ 放真实文件（保留文件名）
│   ├── kptools.exe
│   ├── kpimg                   ← 无后缀
│   ├── msys-2.0.dll
│   └── msys-z.dll              ← 或实际名字（自动扫描 *.dll）
└── ProbeResources/             ← 独立小工具（不参与主程序编译）
    ├── Program.cs              ← 打印 QSLink.exe 内真实资源名
    └── ProbeResources.csproj
```

## 使用步骤

### 1. 放入真实文件
解压 `kptools-msys2-win.7z`（来自 https://github.com/bmax121/KernelPatch/releases ），
把里面的文件放进 `programs\`：
- `kptools.exe`、`kpimg`、`msys-2.0.dll`、以及 msys-z 系列 dll（**文件名保持原样**，会自动扫描全部 *.dll 释放）

### 2. 发布
双击 `publish.bat`，或手动：
```bat
cd /d D:\QSLink
dotnet publish QSLink.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true --force
```
产物：`bin\Release\net8.0\win-x64\publish\QSLink.exe`（单文件）

### 3. 核对资源名（若报错"缺少嵌入资源"）
```bat
dotnet run --project ProbeResources -- "bin\Release\net8.0\win-x64\publish\QSLink.exe"
```
它会打印 exe 内所有资源的真实名称。规则为 `QSLink.programs.{文件名}`。
若名字对不上，改 `Program.cs` 顶部的 `Tools[]` 里 `ResourceNames` 即可。

### 4. 测试（★ 建议在 cmd / PowerShell 里运行）
```bat
QSLink.exe version
QSLink.exe kptools --help
QSLink.exe kptools -k 自定义\kpimg <其他参数>
```

## 三个功能说明
1. `QSLink.exe version` → 显示版本号
2. `QSLink.exe kptools ...` → 运行内嵌 kptools，自动处理 `-k/--kpimg`：
   - 没传 `-k/--kpimg` → 自动追加内置 `programs\kpimg`
   - 传了但后面没路径 / 路径文件不存在 → 改用内置 kpimg
   - 传了且路径有效 → 使用用户的路径
   - kptools 所需的 `msys-2.0.dll` / `msys-z.dll` 自动释放到同目录
3. 每个子程序后**原样透传所有额外参数**

## 常见问题
- **仍弹窗/闪退**：确认是从 cmd/PowerShell 启动（有控制台）。双击虽有 `AllocConsole` 兜底，
  但个别系统上首次分配控制台会闪烁。最稳的做法是始终在命令行里运行。
- **无输出**：先跑 `kptools.exe --help`（直接双击 kptools.exe）确认它本机能输出；
  若 kptools 用更底层的写屏方式，可把 `RunWithFileRedirection` 里改成读控制台句柄（见代码注释）。
- **体积过大**：单文件 exe ≈ 你的代码 + programs\ 下所有文件。若太大先 `dir /s programs`。
