# Gap Audio V1.0.0

完全离线的 Windows 音频编辑、视频音轨提取和批量转换工具。所有解码、剪辑、滤波和导出在本机进行；运行时不下载工具、不连接网络。
## 页面截图
![白天皮肤普通模式编辑音频](/docs/edit1.png)
![黑夜皮肤瀑布模式编辑音频](/docs/edit2.png)
![白天皮肤打开大型视频文件选择音轨](/docs/open1.png)
![白天皮肤批量转换](/docs/outs1.png)


## 程序功能

- 打开音频，或从视频提取一条音轨。多音轨视频先选择要编辑的音轨，列表分行显示标题、default、language、编码和声道布局；文件名右侧可切换音轨，有未保存操作时可选择保存、放弃或取消。
- 简单时间线和高级多行波形；保留/删除区域、撤销、重置、缩放和编辑后播放。导出前只记录操作，不改写源文件。
- RMSE 去全部静音、只去头尾静音，按所有声道计算能量。默认阈值 −36 dB，最短静音 0.1 秒，边界保留 0.3 秒。
- 原格式输出，或转换为 MP3、FLAC；采样率、位深和码率默认“保持”。支持低频消除、高频去除、独立速度和音调调整。速度范围 0.25–4×，音调范围 ±12 半音。
- 修改输出设置后点击“应用处理”生效；待应用时播放仍使用已生效设置。有效频率通过逐声道 8192 点 Hann 窗 FFT 测量，以最强频点的 −60 dB 为界。
- 单/双声道视频默认提取 MP3，多声道视频默认 FLAC。选择 MP3 且超过双声道时，可改用 FLAC 保留声道，或确认混音为双声道 MP3。
- 直接保存、另存为导出、重名自动递增。先生成并验证临时结果，再提交文件；源文件被占用或无法替换时自动另存。视频提取及变更格式、速度、音调必须另存。
- 批量添加文件、递归添加目录，统一应用去静音、滤波、输出格式和速度/音调；可只将勾选项转为 MP3。损坏文件不会中断后续文件，批量导出保留原件。
- 昼夜主题、多实例、主窗口宽度记忆、加载进度/取消、本实例内存与临时磁盘用量。
- 可选当前用户资源管理器菜单：“音频编辑”和“快速转换为 MP3”；Windows 11 中位于“显示更多选项”。快速转换只准备方案，点击导出后才写入文件。移动程序目录后重新开关菜单以更新路径。

## 鼠标与键盘操作

| 操作 | 作用 |
| --- | --- |
| 左键单击时间线/波形 | 定位播放光标 |
| 左键拖拽 | 保留选中时间区间，恢复该区间内的删除标记 |
| 右键拖拽 | 标记删除选中时间区间 |
| 中键拖拽 | 平移视图；简单模式可水平平移，高级模式可纵向平移 |
| 滚轮 | 滚动视图；Shift + 滚轮用于水平滚动 |
| “缩放 ×” | 0.25–20×；简单模式横向扩展，高级模式按行重新排布 |
| 空格 | 播放/暂停（编辑数值或下拉框时除外） |
| Ctrl + Z | 撤销上一步 |
| 拖入单个文件 | 在主窗口打开 |
| 拖入多个文件或目录 | 进入批量编辑 |

简单时间线上方绿色表示保留，下方红色表示删除。高级波形采用相同的鼠标编辑规则。批量列表不支持逐文件手工时间区间剪辑。

## 支持的文件类型

| 用途 | 类型/编码 |
| --- | --- |
| 音频导入 | WAV/WAVE、MP3、FLAC、M4A（AAC/ALAC）、AAC、OGG（Vorbis/Opus）、OPUS、AIFF/AIF、WMA、WavPack（WV）、APE |
| 视频音轨提取 | MP4、M4S、M4V、MOV、MKV、WEBM、AVI、WMV、TS、MTS、M2TS、MPG、MPEG、FLV |
| 视频内的音频编码 | AAC、MP3、MP2、AC-3/E-AC-3、DTS、Vorbis、Opus、FLAC、ALAC、PCM、WMA |
| 音频导出 | WAV/WAVE、MP3、FLAC、M4A（AAC/ALAC）、AAC、OGG、OPUS、AIFF/AIF、WMA、WV |

- 视频只提取所选音轨，不输出画面。容器扩展名不保证内部音频编码一定支持。
- M4S 必须同时包含初始化信息和音频数据；独立的缺少初始化信息分片、只有视频轨的文件、加密音轨不能单独提取。
- WAV 支持常见整数/浮点 PCM、A-law/μ-law、Microsoft ADPCM 和 IMA ADPCM；支持 RF64 大文件读取/输出。
- APE 支持读取并转为可输出格式；没有 APE 编码器，不支持原格式写入。
- 采样率、位深、码率可保持或降低；目标格式不支持的源值按格式约束调整。MP3 最高 48 kHz、320 kbps、双声道；FLAC 当前输出最高 24 bit，有损源转 FLAC 默认 16 bit。
- 无损压缩码率由内容决定，不能固定指定而同时保持无损。有损源转 FLAC 不会恢复已丢失的音质；有损剪辑会重新编码。预览使用单/双声道 PCM，FLAC 导出保留源声道。

## 内存与硬盘使用规则

### 加载与内存

源文件上限 **70 GB**，按 1024 进位计算。超过 **500 MB** 时以主题弹窗询问是否加载，显示估计时间，并提醒先保存办公文档：加载可能造成卡顿、无响应或宕机。超过 5 秒显示加载百分比和“取消本次加载”；取消清理本次临时数据，保留原文件和先前已加载的编辑。

音频先解码为 64 bit 浮点 PCM。解码数据不超过约 128 MB 时存入内存数组；超过时使用磁盘文件映射，波形与静音分析按块读取。映射不等于零内存占用，Windows 仍会缓存读取页；解码、滤波和波形也有额外开销。播放采用小块缓冲，批量逐文件处理。

右下角每秒显示“内存已占用 / 可用”：占用为本实例及其媒体子进程的专用内存；可用为 Windows 当前可用物理内存、提交额度和本进程虚拟地址空间中的较小值，会随其他程序变化，并非固定保留配额。它不代表整台电脑总内存，也不等于所有映射页的物理占用。

### 临时硬盘空间

打开文件会在软件安装目录下的 `temp` 文件夹建立源文件快照、解码 PCM、滤镜配置、预览和测量临时文件。例如默认安装时为 `C:\Program Files (x86)\Gap Audio\temp`；便携版为 `GapAudio.exe` 所在目录下的 `temp`，首次处理时自动创建。安装程序仅给 `temp` 文件夹设置普通用户修改权限，日常运行不需要管理员权限。便携版所在目录应允许写入；无法写入时报告错误，不改用 Windows 用户临时目录。大音频映射文件会保留到替换音频或关闭窗口，小音频读取进内存后删除解码文件。

估算 PCM 空间：

```text
时长（秒） × 采样率（Hz） × 声道数 × 8 字节
```

例如 1 小时、48 kHz、双声道解码约 2.57 GB；还要加原文件快照、预览及处理结果。导出临时文件位于输出目录，需要额外可用空间。磁盘映射模式下右下角追加“磁盘占用”，统计本实例跟踪的解码、源快照和临时处理文件，不包含源文件本体和已完成导出。

小于 1 GB 使用 MB；从 1 GB 起使用 GB，保留两位小数。正常关闭或取消会清理临时数据；强制结束或系统故障可能在安装目录的 `temp` 中留下 `GapGone-*` 文件，确认所有实例已关闭后再清理。安装版卸载时删除整个 `temp` 文件夹及残留内容；不要将需要保留的文件存入该缓存目录。多个实例使用独立文件名，不互相清理正在使用的文件。升级不会清空正在使用的缓存；便携包打包时排除 `temp`。70 GB 是输入检查上限；实际能力受磁盘、内存、编码和硬件影响，**未执行真实 70 GB 全流程压力测试**。

## Windows 与运行库

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / Windows 11，x64 |
| 运行库 | .NET Framework 4.8，或兼容的 4.8.1；本程序不是 .NET 8 应用 |
| 媒体工具 | 随发行包提供的定制 FFmpeg 8.0.1、FFprobe 8.0.1 |
| 其他依赖 | 媒体工具静态构建，仅依赖 Windows 系统 DLL，无需单独安装 VC++ 运行库 |
| 网络 | 运行完全离线；获取源码/工具及搭建开发环境可联网 |

Release 提供两种运行包：

- 安装版 `GapAudio-V1.0.0-Windows-x64-Setup.exe`：中文现代安装向导，跟随 Windows 明暗主题；默认目录为 `C:\Program Files (x86)\Gap Audio`，可修改。桌面快捷方式默认勾选，可取消；同时创建开始菜单快捷方式，支持从 Windows“已安装的应用”卸载。写入默认目录需要管理员权限，日常运行不需要管理员权限。
- 便携版 `GapAudio-V1.0.0-Windows-x64.zip`：解压后运行 `GapAudio.exe`。保留压缩包中的所有文件，尤其是同目录的 `ffmpeg.exe`、`ffprobe.exe` 和许可说明；解压和运行不需要管理员权限。

两种运行包均包含媒体工具，安装及运行不联网。安装包检查 .NET Framework 4.8/4.8.1，缺少时提示先安装运行库，不自动下载。资源管理器集成只写当前用户注册表。Windows 7/8、32 位 Windows 和原生 ARM64 不在本发行支持范围。

## 开发技术栈与版本

| 技术 | 版本/用途 |
| --- | --- |
| C# | C# 5 语法；主程序、数据模型、算法、异步任务 |
| .NET Framework | 目标 4.8（net48）；系统构建使用 csc 4.8.9232.0 |
| WinForms / System.Drawing | 桌面窗口、主题控件、时间线与波形 |
| Windows waveOut / Win32 P/Invoke | 音频播放、内存信息和资源管理器集成 |
| MemoryMappedFile | 大音频磁盘映射 |
| System.Numerics | FFT 的复数计算 |
| FFmpeg / FFprobe | 8.0.1；音频编解码、滤镜与容器探测 |
| PowerShell | 构建脚本兼容 Windows PowerShell 5.1 |
| Inno Setup | 安装包使用 7.1.0；现代明暗主题安装向导、快捷方式与卸载 |

程序版本在 `src/AppIdentity.cs`；程序集/文件版本为 `1.0.0.0`，产品显示版本为 `V1.0.0`。媒体第三方库与工具链版本见 [MEDIA-BUILD.md](MEDIA-BUILD.md) 和 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。本 Windows 实现无需 Node.js、npm、Rust、Tauri 或 Python。

## 开发环境与编译

### 编译主程序

使用 Windows 10/11 x64、.NET Framework 4.8 和 Windows PowerShell 5.1。构建脚本使用系统的：

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

无需安装 .NET SDK。也可用支持 net48 的 Visual Studio 配合 .NET Framework 4.8 Developer Pack 打开 `GapAudio.csproj`；仓库验证使用下面的 PowerShell 流程。

1. 在源码仓库根目录打开 PowerShell，也就是包含 `GapAudio.csproj` 和 `build.ps1` 的目录；若使用独立源码附件，直接进入附件解压目录。下列命令和相对路径均以该目录为起点。
2. 将同版本运行发行包解压到本目录的 `bin`，使 `bin/ffmpeg.exe` 和 `bin/ffprobe.exe` 存在。也可按 [媒体构建说明](MEDIA-BUILD.md) 重新构建这两个工具。
3. 关闭要覆盖的 `GapAudio.exe`，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

输出为 `bin/GapAudio.exe`。构建会由 `resources/AppAssets.resx` 生成资源并嵌入程序；`resources/app.ico` 是 EXE 图标。不自动下载任何依赖，修改 C# 不需要重编 FFmpeg。

### 测试与打包

同样在源码仓库根目录（或独立源码附件根目录）运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\package-installer.ps1
```

测试需要发行目录中的媒体工具，涵盖真实编解码、音轨、播放设备、窗口布局、宽度记忆、主题和取消。保留 `tests/CoreTests.cs` 与小型 `tests/video-fixtures/`；生成的测试 EXE、日志及 `tests/runs/` 不提交。验证记录见 [VALIDATION.md](VALIDATION.md)。

发行 ZIP 输出为 `dist/GapAudio-V1.0.0-Windows-x64.zip`；安装包输出为 `dist/GapAudio-V1.0.0-Windows-x64-Setup.exe`。安装包构建需要 [Inno Setup 7.1 或更高版本](https://jrsoftware.org/isdl.php)，脚本自动查找已安装的 `ISCC.exe`；非标准目录也可通过 `-InnoCompiler 'D:\Inno Setup 7\ISCC.exe'` 指定。构建过程不下载工具，修改安装配置见 [installer/README.md](installer/README.md)。

`bin`、`dist` 和 `tools` 均在 `.gitignore` 中，应将运行包与对应媒体源码附件放在 Git Release，而非提交生成二进制到源码仓库。

### 主窗口宽度

窗口正常关闭时保存普通窗口宽度，下次启动恢复；最大化或最小化关闭时保留此前普通宽度。设置文件为 `%LOCALAPPDATA%\GapAudio\window-width.txt`，多实例以最后关闭的窗口为准。无有效设置时使用 `src/MainForm.cs` 的 `InitialWindowWidth = 1280`，最小宽度为 960 像素。修改默认值后重新编译；删除宽度设置可恢复默认值。

## 源码目录与清理

```text
仓库根目录/
  README.md / LICENSE            仓库说明与 GPLv3 许可
  src/                           C# 源码
  resources/                     图标与嵌入资源源文件
  docs/                          文档截图
  media/                         FFmpeg 配置记录与第三方许可
  tests/CoreTests.cs             回归验证源码
  tests/video-fixtures/          小型测试视频素材
  GapAudio.csproj                net48 项目
  build.ps1 / test.ps1            编译与测试
  package.ps1                    生成 Release ZIP
  package-installer.ps1          生成 Inno Setup 安装包
  installer/                     安装配置、品牌图生成脚本与说明
  build-media.ps1 / .sh           可选媒体工具重建脚本
  MEDIA-BUILD.md                  媒体构建说明
  THIRD-PARTY-NOTICES.md          第三方组件许可说明
  VALIDATION.md                  验证记录
  .gitignore                     Git 排除规则
  bin/                           本地运行文件（忽略 Git）
  dist/                          安装包、ZIP 和打包产物（忽略 Git）
  tools/                         媒体开发工具链与缓存（忽略 Git）
```

所有项目文件已位于仓库根目录，直接运行根目录的构建脚本即可。`.gitignore` 按当前结构排除根目录的 `bin/`、`dist/`、`tools/`、`temp/` 和 `release-*/`，以及 `tests/runs/`、测试 EXE/日志、生成资源和编译/编辑器缓存。`src/`、`resources/` 的源文件、`media/`、`installer/`、`docs/` 和小型测试素材应保留并提交。

**tools 可以删除。** 测试源码和小型 video-fixtures 应保留，历史 tests/runs 可以删除。 它只包含可重新准备的 MSYS2、媒体源码解压目录、对象文件、下载包和构建缓存；运行程序、测试和重编 C# 只需现成的 `bin` 媒体工具。需要重建 FFmpeg 时再按构建说明准备 `tools`。删除前保留对应媒体源码附件、构建配置及许可文件。

本次整理保留源码、必要测试素材和许可，删除旧项目/配置、测试可执行文件和日志。tools、历史 tests/runs 和重复发行目录属于本机缓存，已忽略 Git 提交；本次自动递归删除被执行策略阻止，仍需在本机删除后才能释放空间。发行二进制和对应源码附件留在本机 dist，不纳入 Git 提交。

## 算法参考与许可

部分算法来源及功能参考：[LiRenTech/gap-gone](https://github.com/LiRenTech/gap-gone)，包括静音能量检测与音频编辑的相关思路。本仓库为 C#/WinForms 的 Windows 实现，采用独立的本地媒体处理流程。

应用遵循 [GNU GPLv3](LICENSE)。定制 FFmpeg 使用 GPLv2 或更高版本配置，其他组件许可见第三方说明。发布运行包时同时提供许可文件，并在同一 Release 提供 `GapAudio-V1.0.0-Media-Source.zip`：含应用源码、FFmpeg 8.0.1 原始源码、对应第三方版本源包/补丁和重建脚本。不要只发布二进制而丢弃其对应源码附件。
