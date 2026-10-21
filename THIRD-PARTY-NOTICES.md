# 第三方媒体组件

本发行使用 FFmpeg 8.0.1 定制静态构建，许可证为 GPLv2 或更高版本。应用项目许可证以 LICENSE（GPLv3）为准。全部组件在本地运行，不访问网络。

| 组件 | 版本 | 许可证 | 对应源码 |
| --- | --- | --- | --- |
| FFmpeg | 8.0.1 | GPLv2+（本配置） | https://ffmpeg.org/releases/ffmpeg-8.0.1.tar.xz |
| LAME | 3.100-3 | LGPLv2+ | https://repo.msys2.org/mingw/sources/mingw-w64-lame-3.100-3.src.tar.zst |
| Vorbis | 1.3.7-3 | BSD | https://repo.msys2.org/mingw/sources/mingw-w64-libvorbis-1.3.7-3.src.tar.zst |
| Ogg | 1.3.6-1 | BSD | https://repo.msys2.org/mingw/sources/mingw-w64-libogg-1.3.6-1.src.tar.zst |
| Opus | 1.6.1-2 | BSD | https://repo.msys2.org/mingw/sources/mingw-w64-opus-1.6.1-2.src.tar.zst |
| Rubber Band | 3.3.0-2 | GPLv2+ | https://repo.msys2.org/mingw/sources/mingw-w64-rubberband-3.3.0-2.src.tar.zst |
| FFTW | 3.3.11-2 | GPLv2+ | https://repo.msys2.org/mingw/sources/mingw-w64-fftw-3.3.11-2.src.tar.zst |
| libsamplerate | 0.2.2-1 | BSD | https://repo.msys2.org/mingw/sources/mingw-w64-libsamplerate-0.2.2-1.src.tar.zst |

许可全文在源码仓库 media/FFmpeg-LICENSE.txt、media/licenses/ 中；运行包包含 FFmpeg-LICENSE.txt、licenses/。工具链为 MSYS2 UCRT64 GCC 16.2.0，静态链接 GCC 运行库适用 GCC Runtime Library Exception；系统依赖仅 Windows 自带 DLL。

GapAudio-V1.0.0-Media-Source.zip 为本轮构建准备的对应媒体源码附件，包含 FFmpeg 源码包、MSYS2 的对应版本源包（含构建补丁/配方）、自定义 build-media.sh、build-media.ps1、MEDIA-BUILD.md、最终配置和源码哈希。该附件不参与运行，发布时与运行包放在同一 Git Release；本机文件保存在仓库根目录的 dist 中，忽略 Git 提交。应用源码位于仓库根目录下的 src，媒体配置与许可位于 media。

FFmpeg 配置与编译说明见 MEDIA-BUILD.md、media/FFmpeg-BUILD.txt、media/FFmpeg-config.h。对 FFmpeg 源码未作修改。

部分算法来源及功能参考：[LiRenTech/gap-gone](https://github.com/LiRenTech/gap-gone)。应用许可见 LICENSE（GNU GPLv3）。

安装程序由 [Inno Setup 7.1.0](https://jrsoftware.org/) 生成，使用其原生现代明暗主题向导。Inno Setup 版权归 Jordan Russell、Martijn Laan 等作者所有，其许可允许用于商业及非商业产品；完整许可见 [Inno Setup 许可](https://jrsoftware.org/files/is/license.txt)。安装构建工具无需随程序安装。
