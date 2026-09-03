# QShell
文档切换： [Android](README.md)|Windows
- 一个在Windows中用于对Android执行部分操作的脚本

# 功能特性
  - 支持解锁部分OnePlus/Google机型的Bootloader
  - 通过adb进行隐藏Root环境灯操作
  - 通过adb+fastboot嵌入KPM(APatch)
  - 备份镜像文件

# 支持环境
  - 系统Windows 10 20H2+
  - 终端：PowerShell 5.0+

# 使用方法
1.下载脚本
``` PowerShell
Invoke-WebRequest -Uri https://github.com/qimgss/QShell/raw/refs/heads/main/QShell.ps1 -OutFile .\QShell.ps1
```
2.允许运行Powershell脚本
在PowerShell中执行以下
``` PowerShell
Start-Process ms-settings:developers
```
3.运行脚本
``` PowerShell
.\QShell.ps1
```

# QSLink
  - 这是一个包含githubdl magiskboot yq与KernelPatch的工具，你可以这样使用这个工具：
  ```
    .\QSLink.exe githubdl args...
  ```