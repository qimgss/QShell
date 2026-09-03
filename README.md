# QShell
文档切换： Android|[Windows](readme_win.md)
- 一个用于隐藏Android设备的Root环境的脚本

# 功能特性
  - 多种Root实现方式的支持：支持Magisk KernelSU APatch
  - 安装隐藏Root环境所需模块：从仓库中的配置文件下载模块

# 支持环境
  - 系统：Android 12.0以上
  - Root实现方式：Magisk(28.0+)、KernelSU(2.0.0+)、APatch(11142+)
  - 终端：任意

# 使用方法
1.下载脚本
``` bash
  curl -LJO https://github.com/qimgss/QShell/raw/refs/heads/main/QShell.sh
```
2.以root权限运行脚本
``` bash
  su -c "sh QShell.sh"
```

# QSBox
  - 一个整合了bash blkops githubdl magiskboot yq与KernelPatch的工具，你可以这样使用这个工具：
  ```
    qsbox bash args....
  ```