# FFmpeg 8.0.1 精简工具重建

此流程仅用于重新构建 `ffmpeg.exe` 和 `ffprobe.exe`。运行 Gap Audio、执行测试或编译 C# 主程序时不需要 `tools`，可使用同版本 Release 的预编译工具。

## 版本

| 组件 | 使用版本 |
| --- | --- |
| FFmpeg / FFprobe | 8.0.1 |
| MSYS2 工具链 | UCRT64，GCC 16.2.0（Rev6） |
| GNU Make | 4.4.1 |
| LAME | 3.100，MSYS2 包修订 3 |
| Vorbis / Ogg | 1.3.7-3 / 1.3.6-1 |
| Opus | 1.6.1-2 |
| Rubber Band | 3.3.0-2 |
| FFTW | 3.3.11-2 |
| libsamplerate | 0.2.2-1 |

源包的准确版本、补丁和 SHA256 见 Release 的 `GapAudio-V1.0.0-Media-Source.zip`。MSYS2 滚动更新仓库中的最新包可能与上述版本不同，精确复现应使用对应源码包和相同工具链。

## 环境准备

下列相对路径以源码仓库根目录（包含 `build-media.ps1` 的目录）为起点。在 Windows x64 下准备 MSYS2，放入 `tools/msys64`。准备 UCRT64 GCC、pkgconf、LAME、Vorbis/Ogg、Opus、Rubber Band（及其 FFTW/libsamplerate 依赖）、GNU Make、NASM 和 diffutils。可在 MSYS2 环境中安装：

```bash
pacman -S --needed mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-pkgconf \
  mingw-w64-ucrt-x86_64-lame mingw-w64-ucrt-x86_64-libvorbis \
  mingw-w64-ucrt-x86_64-opus mingw-w64-ucrt-x86_64-rubberband \
  mingw-w64-ucrt-x86_64-fftw mingw-w64-ucrt-x86_64-libsamplerate \
  make nasm diffutils
```

环境搭建可联网；编译脚本本身不下载依赖。将官方 [ffmpeg-8.0.1.tar.xz](https://ffmpeg.org/releases/ffmpeg-8.0.1.tar.xz) 解压到 `tools/ffmpeg-8.0.1`，保留目录里的 configure 和许可文件。

## 编译

在源码仓库根目录（或独立源码附件的根目录）执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-media.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

构建配置在 [build-media.sh](build-media.sh)，缓存输出到 `tools/ffmpeg-audio-build`，最终程序复制到 `bin`。首次主程序构建会补充 `media` 中保存的许可全文。

配置仅启用所需音频解码/编码、容器、剪辑、重采样、滤波和 Rubber Band 处理；协议只有 file/pipe，不包含视频编码器、硬件加速或网络协议。MPEG 视频解析器用于正确识别 MPEG-PS 音轨。测试视频已预生成，无需把视频编码器加入发行。

使用 `-Os`、静态链接、函数节回收和符号剥离，不使用 EXE 压缩器。不要增加 `-fdata-sections`：此工具链会将 FFT 表写入文件数据节而显著增大 EXE。

仓库记录位于 `media/FFmpeg-BUILD.txt`、`media/FFmpeg-config.h`、`media/FFmpeg-LICENSE.txt` 和 `media/licenses/`；每次构建也会在 `bin` 生成当前配置记录。版本改变后应重新检查许可和对应源码，并更新 Release 附件。

## 删除 tools

确认 `bin/ffmpeg.exe`、`bin/ffprobe.exe` 和对应源码附件已保留后，可删除整个 `tools`。它是忽略提交的开发缓存，不参与运行。以后只有重新构建媒体工具才需要重新准备。
