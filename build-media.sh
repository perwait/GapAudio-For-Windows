#!/usr/bin/env bash
set -euo pipefail
export PATH="/ucrt64/bin:/usr/bin:$PATH"
export PKG_CONFIG_PATH=/ucrt64/lib/pkgconfig
project=$(cd "$(dirname "$0")" && pwd)
source_dir="$project/tools/ffmpeg-8.0.1"
build_dir="$project/tools/ffmpeg-audio-build"
output_dir="${1:-$project/bin}"
mkdir -p "$build_dir" "$output_dir"
cd "$build_dir"
"$source_dir/configure" \
  --prefix="$build_dir/install" --target-os=mingw32 --arch=x86_64 \
  --cc=gcc --cxx=g++ --pkg-config=pkgconf --pkg-config-flags=--static \
  --disable-autodetect --disable-everything --disable-doc --disable-debug \
  --disable-network --disable-avdevice --disable-swscale \
  --disable-ffplay --disable-shared --enable-static --enable-small --enable-gpl \
  --enable-ffmpeg --enable-ffprobe --enable-avcodec --enable-avformat \
  --enable-avfilter --enable-swresample --enable-w32threads \
  --enable-libmp3lame --enable-libvorbis --enable-libopus --enable-librubberband \
  --enable-protocol=file,pipe \
  --enable-demuxer=wav,mp3,flac,mov,aac,ogg,aiff,asf,wv,ape,matroska,avi,mpegts,mpegps,mpegvideo,flv \
  --enable-muxer=wav,mp3,flac,ipod,adts,ogg,opus,aiff,asf,wv,pcm_f64le \
  --enable-decoder=pcm_u8,pcm_s8,pcm_s16le,pcm_s16be,pcm_s24le,pcm_s24be,pcm_s32le,pcm_s32be,pcm_f32le,pcm_f32be,pcm_f64le,pcm_f64be,pcm_alaw,pcm_mulaw,adpcm_ms,adpcm_ima_wav,mp3,mp3float,mp2,mp2float,flac,aac,aac_latm,alac,vorbis,opus,wmav1,wmav2,wmapro,wmalossless,wavpack,ape,ac3,eac3,dca \
  --enable-encoder=pcm_u8,pcm_s8,pcm_s16le,pcm_s16be,pcm_s24le,pcm_s24be,pcm_s32le,pcm_s32be,pcm_f32le,pcm_f64le,libmp3lame,flac,aac,alac,libvorbis,libopus,wmav2,wavpack \
  --enable-parser=mpegaudio,mpegvideo,aac,aac_latm,ac3,dca,flac,opus,vorbis \
  --enable-bsf=aac_adtstoasc,extract_extradata \
  --enable-filter=abuffer,abuffersink,aformat,anull,asplit,atrim,asetpts,concat,highpass,lowpass,rubberband,aresample \
  --extra-cflags='-Os -ffunction-sections' \
  --extra-ldflags='-static -static-libgcc -static-libstdc++ -Wl,--gc-sections'
make -j "${NUMBER_OF_PROCESSORS:-4}" ffmpeg.exe ffprobe.exe
strip ffmpeg.exe ffprobe.exe
cp ffmpeg.exe ffprobe.exe "$output_dir/"
cp "$source_dir/COPYING.GPLv2" "$output_dir/FFmpeg-LICENSE.txt"
cp config.h "$output_dir/FFmpeg-config.h"
./ffmpeg.exe -version > "$output_dir/FFmpeg-BUILD.txt" 2>&1
echo "Built offline audio tools: $output_dir"
