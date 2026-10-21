# Gap Audio 安装包

使用 Inno Setup 7.1.0 构建；需要 7.1 或更高版本。无需 Node.js、外部 UI 插件或在线安装服务。

## 构建

先按 [项目 README](../README.md) 准备 .NET Framework 4.8、Windows PowerShell 5.1，以及仓库根目录下的 `bin/ffmpeg.exe` 和 `bin/ffprobe.exe`。安装 [Inno Setup](https://jrsoftware.org/isdl.php)，在源码仓库根目录（包含 `package-installer.ps1` 的目录）执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package-installer.ps1
```

脚本从 PATH、常见目录和卸载注册表自动查找 ISCC；也可明确指定：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\package-installer.ps1 -InnoCompiler 'D:\Inno Setup 7\ISCC.exe'
```

输出：仓库根目录下的 `dist/GapAudio-V1.0.0-Windows-x64-Setup.exe`。脚本先重新编译主程序，再用根目录的 `logo.png` 生成明暗安装图，最后压缩所需运行文件；不会下载任何依赖。生成图位于 `dist/installer-assets/`，安装包和生成图均忽略 Git 提交。安装配置为 `installer/GapAudio.iss`，图像生成脚本为 `installer/generate-assets.ps1`；ISS 中的相对路径以 `installer/` 为起点。

## 配置与行为

`GapAudio.iss` 保存可提交 Git 的安装配置：

- `DefaultDirName={commonpf32}\Gap Audio`：通常展开为 `C:\Program Files (x86)\Gap Audio`；使用独立子目录，用户可在目录页修改。升级默认沿用之前的安装位置。
- `WizardStyle=modern dynamic windows11 includetitlebar`：中文现代向导，跟随 Windows 明暗主题，含 logo 和品牌侧栏。
- `Tasks` 中 `desktopicon` 默认选中；没有 `unchecked` 标志。`UsePreviousTasks=no` 使每次打开安装向导均默认勾选，用户可取消。开始菜单快捷方式自动创建。
- 默认以管理员模式安装，支持 Windows 10/11 x64；程序仍为 x64，即使安装在 Program Files (x86)。`/CURRENTUSER` 仅供指定可写目录时的部署/隔离验证，默认仍是管理员模式。
- 安装前检测 .NET Framework 4.8/4.8.1；缺少时显示提示退出，不下载运行库。FFmpeg 和 FFprobe 已包含在安装包中。
- 固定 AppId 标识产品，用于识别更新和卸载。后续版本应保留 AppId，同步更新 ISS 与 `src/AppIdentity.cs` 的版本字段。
- 安装创建 `{app}\temp`，通过 `Permissions: users-modify` 仅赋予缓存目录普通用户修改权限，程序文件目录保留原权限。加载快照、解码、滤镜、预览和测量文件全部使用该目录；升级保留缓存。用户可正常运行程序，无需管理员权限。
- 卸载移除安装器写入的程序文件、快捷方式和卸载记录，并通过 `[UninstallDelete]` 删除 `{app}\temp` 及其全部内容；不递归删除整个安装目录。用户音频导出和用户配置不主动删除；不要将需要保留的文件存入 `temp`。
- 资源管理器右键菜单由程序自行写入当前用户注册表。启用过该功能的用户，应在卸载前在程序中关闭该选项，以免留下指向已卸载路径的菜单。

重新发布时同时提供对应 `GapAudio-V1.0.0-Media-Source.zip`，保留完整应用/媒体许可与源码。安装程序尚未配置代码签名。
