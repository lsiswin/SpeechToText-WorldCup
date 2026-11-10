# 🎙️ 智能多功能媒体工具箱 (SpeechToText Media Toolbox)

这是一个基于 **Vue 3 + Vite** 前端与 **ASP.NET Core 8.0 (C#) Web API** 后端构建的模块化多功能媒体处理与语音识别工具箱。界面采用深邃优雅的暗黑科技风，配备高保真玻璃微光质感与微交互动效。

本系统集成了本地离线语音转文字（Whisper.net）、在线视频多格式解析与下载（yt-dlp + FFmpeg）、纯音频提取、以及视频智能翻译与硬字幕压制等核心模块。

---

## ✨ 核心功能模块

### 🎙️ 1. 本地音视频转文字 (Whisper 离线版)
* **全离线语音识别**：内置 `Whisper.net` 库（支持 `Tiny` / `Base` / `Small` / `Medium` 模型），首次运行自动下载模型，数据全部在本地处理，安全可靠。
* **高兼容格式转换**：自动检测 / 部署本地 FFmpeg，将任意音视频格式转为标准 16kHz PCM 单声道 WAV 格式进行高精识别。
* **富文本交互校对**：段落按精确时间轴展示，支持点选播放、波形寻道、双击修改文本内容。
* **多格式一键导出**：支持导出美观的 Word (.docx)、SRT 字幕及普通 TXT 文本。

### 📥 2. 在线视频提取与转录 (YouTube & Bilibili)
* **智能链接解析与封面代理**：支持 YouTube（含 Shorts）与 Bilibili（BV/av/`b23.tv`）链接解析；后端内置图片代理缓存接口与自动系统代理探测，解决跨区外网封面加载失败问题。
* **多画质 / 多容器视频下载**：基于 **`yt-dlp` + `FFmpeg`**，支持下载原画最高（4K）、1080P、720P、480P 视频流，支持封装为 **MP4** 或 **MKV** 容器。
* **纯音频提取与转码**：直接提取音轨并转码保存为 **MP3 (320kbps)**、**M4A (AAC)**、**WAV (无损)** 或 **FLAC**。
* **一键语音转写与字幕压制**：解析视频后可直接调入 Whisper 本地引擎生成带时间轴文本，或直接一键压制硬字幕。

### 🎬 3. 视频智能字幕压制与翻译 (DeepSeek / Google)
* **智能双语翻译**：可选免密钥 Google 翻译或接入 **DeepSeek API** 进行高精准度语境翻译。
* **ASS 高保真字幕生成**：支持自定义字幕视觉样式（黑金底框 / 白字微光 / 赛博青霓虹）、字号大小及显示位置，调用 FFmpeg 烧录输出高清硬字幕 MP4。
* **实时任务监控**：支持实时百分比进度追踪与任务取消。

---

## 🛠️ 技术栈

* **前端**：`Vue 3` + `Vite` + `Vanilla CSS`
* **后端**：`ASP.NET Core 8.0` + `Whisper.net` + `yt-dlp` + `Open-XML (Word)` + `FFmpeg` 自动部署器

---

## 🚀 快速启动指南

为了方便您的使用，项目根目录下已为您编写好了一键批处理启动脚本：

### 1. 服务一键启动
* 双击运行根目录下的 **`run-all.bat`**：一键安装所需依赖，并同时唤起前端与后端服务。
* *或手动启动*：
  * 后端启动：双击运行 `run-backend.bat`（或在 `backend/SpeechToText.Api/` 执行 `dotnet run`）
  * 前端启动：双击运行 `run-frontend.bat`（或在 `frontend/` 执行 `npm run dev`）

打开浏览器访问：`http://localhost:5173` 即可进入主页。

### 2. 配置 DeepSeek API 密钥（可选）
本系统对密钥文件进行了安全保护，绝不会提交到公共仓库。若您想使用 DeepSeek AI 翻译功能：
1. 在 `backend/SpeechToText.Api/` 文件夹下新建文件 **`appsettings.local.json`**。
2. 写入以下内容：
   ```json
   {
     "Translation": {
       "Provider": "DeepSeek",
       "ApiKey": "您的真实DeepSeekAPI密钥"
     }
   }
   ```
3. 保存后重启后端服务即可。

---

## 💡 反馈与贡献

本仓库持续维护和更新，欢迎通过 GitHub Issues 或 Pull Requests 提交想法与建议。
