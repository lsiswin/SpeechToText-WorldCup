<template>
  <div class="glass-panel online-input-container" :style="{ '--platform-color': themeColor }">
    <!-- Input Section -->
    <div class="input-section" v-if="!videoInfo">
      <div class="url-input-wrapper">
        <div class="input-icon">
          <!-- Multi-platform Icon -->
          <svg v-if="detectedPlatform === 'bilibili'" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="platform-svg-icon bili-icon animate-pop">
            <path d="M17.844 2.25a.75.75 0 0 1 .715.986l-1.042 2.923a9.71 9.71 0 0 1 3.535 2.12c2.454 2.302 2.766 5.86 2.877 7.747a1.503 1.503 0 0 1-1.39 1.583c-1.309.117-6.059.391-9.539.391-3.48 0-8.23-.274-9.539-.39a1.503 1.503 0 0 1-1.39-1.584c.11-1.887.423-5.445 2.877-7.747A9.71 9.71 0 0 1 7.48 6.16L6.44 3.236a.75.75 0 1 1 1.41-.5l1.32 3.7a9.7 9.7 0 0 1 5.66 0l1.32-3.7a.75.75 0 0 1 .694-.486zM6.9 10.375c-.958.9-1.258 2.502-1.365 3.993.418.026.96.046 1.587.058.468.01 1.01.013 1.614.01 1.21-.005 2.65-.034 4.095-.084l-.16-1.53c-1.066.036-2.146.06-3.153.064-.473.003-.923.001-1.343-.007a12.7 12.7 0 0 1-1.036-.041c.062-.84.225-1.78.761-2.283.473-.443 1.253-.591 2.327-.565l.036-1.53c-1.464-.035-2.738.163-3.627.995zm10.2 0c-.89-.832-2.163-1.03-3.627-.995l.036 1.53c1.074-.026 1.854.122 2.327.565.536.503.7 1.442.761 2.283a12.7 12.7 0 0 1-1.036.041c-.42.008-.87.01-1.343.007-1.007-.004-2.087-.028-3.153-.064l-.16 1.53c1.446.05 2.885.08 4.095.084.604.003 1.146 0 1.614-.01.628-.012 1.17-.032 1.587-.058-.107-1.49-.407-3.093-1.365-3.993z"/>
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="platform-svg-icon yt-icon animate-pop">
            <path d="M23.498 6.163a3.003 3.003 0 0 0-2.11-2.108C19.524 3.545 12 3.545 12 3.545s-7.525 0-9.388.51a3.002 3.002 0 0 0-2.11 2.108C0 8.029 0 12 0 12s0 3.972.502 5.837a3.003 3.003 0 0 0 2.11 2.108c1.863.51 9.388.51 9.388.51s7.525 0 9.388-.51a3.002 3.002 0 0 0 2.11-2.108C24 15.972 24 12 24 12s0-3.973-.502-5.837zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/>
          </svg>
        </div>
        <input 
          type="text" 
          v-model="onlineUrl" 
          placeholder="请输入 YouTube 或 B站 视频链接..."
          class="url-input-box"
          :disabled="isLoadingInfo || isTranscribing"
          @input="handleUrlInput"
        />
        <button 
          v-if="onlineUrl && !isLoadingInfo && !isTranscribing" 
          class="clear-input-btn" 
          @click="clearAll"
        >×</button>
        <div v-if="isLoadingInfo" class="mini-spinner"></div>
      </div>
      <p class="input-hint">支持标准 YouTube 链接、B站 BV/av 链接以及 b23.tv 短链接</p>
      
      <!-- Fetch Error Alert -->
      <div v-if="fetchError" class="fetch-error-box">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="error-svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
        <span>视频解析失败: {{ fetchError }} (请检查网络代理或输入链接)</span>
      </div>
    </div>

    <!-- Active Video Card & Actions Selection -->
    <div v-else class="video-preview-flow animate-fade-in">
      <!-- Back Button -->
      <div class="top-nav-bar">
        <button class="back-link-btn" @click="clearAll" :disabled="isTranscribing || isDownloading">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-back-icon">
            <path fill-rule="evenodd" d="M9.707 16.707a1 1 0 01-1.414 0l-6-6a1 1 0 010-1.414l6-6a1 1 0 011.414 1.414L5.414 9H17a1 1 0 110 2H5.414l4.293 4.293a1 1 0 010 1.414z" clip-rule="evenodd" />
          </svg>
          返回输入链接
        </button>

        <span class="source-platform-pill" :class="videoInfo.platform">
          {{ videoInfo.platform === 'bilibili' ? '哔哩哔哩 (Bilibili)' : 'YouTube 国际媒体' }}
        </span>
      </div>

      <!-- Video Preview Card (Cover / Title) -->
      <div class="video-info-card">
        <div class="video-cover-wrapper">
          <!-- Proxied Cover Image with graceful fallback -->
          <img 
            v-if="videoInfo.thumbnailUrl && !coverError"
            :src="getProxiedImageUrl(videoInfo.thumbnailUrl)" 
            class="video-cover" 
            alt="Video Thumbnail" 
            referrerpolicy="no-referrer"
            @error="coverError = true"
          />
          <div v-else class="video-cover-fallback" :class="videoInfo.platform">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="fallback-cover-icon">
              <path d="M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zm0 2v12h16V6H4zm2 2h3v2H6V8zm0 4h3v2H6v-2zm0 4h3v2H6v-2zm12-8h-7v6h7V8zm-2 2v2h-3v-2h3zm2 4h-7v2h7v-2z"/>
            </svg>
            <span class="fallback-text">{{ videoInfo.platform === 'bilibili' ? 'B站视频' : 'YouTube' }}</span>
          </div>

          <span class="video-duration-tag">{{ formatDuration(videoInfo.durationSeconds) }}</span>
          <!-- Platform Tag Overlay -->
          <span class="platform-badge-tag" :class="videoInfo.platform">
            {{ videoInfo.platform === 'bilibili' ? 'Bilibili' : 'YouTube' }}
          </span>
        </div>

        <div class="video-details-wrapper">
          <h3 class="video-preview-title" :title="videoInfo.title">{{ videoInfo.title }}</h3>
          
          <div class="creator-profile-box">
            <div class="creator-info-inline">
              <!-- Proxied Creator Avatar with graceful fallback -->
              <img 
                v-if="videoInfo.avatarUrl && !avatarError" 
                :src="getProxiedImageUrl(videoInfo.avatarUrl)" 
                class="creator-avatar" 
                referrerpolicy="no-referrer"
                @error="avatarError = true"
              />
              <div v-else class="creator-avatar-fallback" :class="videoInfo.platform">
                {{ (videoInfo.author || 'UP').charAt(0).toUpperCase() }}
              </div>
              <span class="creator-name" :title="videoInfo.author">{{ videoInfo.author }}</span>
            </div>

            <div class="cover-actions-group">
              <button class="download-cover-btn" @click="copyVideoUrl" title="复制视频原始链接">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="cover-dl-icon">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                </svg>
                <span>{{ copiedUrl ? '已复制' : '复制链接' }}</span>
              </button>

              <button class="download-cover-btn" @click="downloadCover" title="保存视频高清封面原图">
                <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="cover-dl-icon">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                </svg>
                <span>下载封面</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Action Choice Container (4 distinct functional pathways) -->
      <div class="choices-container" v-if="!isTranscribing && !isDownloading">
        <!-- Pathway 1: Speech to Text Options -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'transcribe' }">
          <div class="choice-header" @click="activeChoice = 'transcribe'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'transcribe' }"></span>
            </div>
            <div class="choice-info">
              <div class="choice-title-row">
                <h4>🎙️ 语音转写文字 (Speech to Text)</h4>
                <span class="choice-badge">本地离线 AI</span>
              </div>
              <p>高速抓取高质量音轨，通过本地 Whisper 引擎生成带精确时间轴的文本，支持回放与词句校对。</p>
            </div>
          </div>

          <!-- Drawer 1: Transcribe Options -->
          <div class="choice-drawer" v-if="activeChoice === 'transcribe'">
            <div class="option-row">
              <div class="option-header-row">
                <label class="label-heading">Whisper 离线模型</label>
                <span class="option-hint-text">模型越大识别精度越高，中英文混合效果更佳</span>
              </div>
              <div class="model-grid-compact">
                <div 
                  v-for="model in models" 
                  :key="model.value" 
                  class="model-card-compact"
                  :class="{ 'active': selectedModel === model.value }"
                  @click="selectedModel = model.value"
                >
                  <div class="model-header-compact">
                    <span class="model-name-compact">{{ model.name }}</span>
                    <span class="model-size-compact">{{ model.size }}</span>
                  </div>
                  <span class="model-desc-text">{{ model.desc }}</span>
                </div>
              </div>
            </div>
            
            <button class="btn btn-primary start-btn" @click="startTranscription">
              <span>🚀 开始语音识别与转写</span>
            </button>
          </div>
        </div>

        <!-- Pathway 2: Download Video File -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'download' }">
          <div class="choice-header" @click="activeChoice = 'download'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'download' }"></span>
            </div>
            <div class="choice-info">
              <div class="choice-title-row">
                <h4>🎬 直接下载视频 (Download Video)</h4>
                <span class="choice-badge">无损音画合成</span>
              </div>
              <p>完整提取视频与音频分轨，利用本地高性能 FFmpeg 自动化快速封包，输出纯净视频文件。</p>
            </div>
          </div>

          <!-- Drawer 2: Video Download Options -->
          <div class="choice-drawer" v-if="activeChoice === 'download'">
            <!-- Resolution Selection -->
            <div class="option-row">
              <label class="label-heading">选择下载画质</label>
              <div class="quality-grid">
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === 'best' }"
                  @click="selectedQuality = 'best'"
                >
                  <div class="quality-top-row">
                    <span class="quality-label">原画最高 / 4K</span>
                    <span class="mini-tag gold">最高品质</span>
                  </div>
                  <span class="quality-desc">DASH 原画 · 最高可用分辨率</span>
                </div>
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === '1080p' }"
                  @click="selectedQuality = '1080p'"
                >
                  <div class="quality-top-row">
                    <span class="quality-label">1080P 全高清</span>
                    <span class="mini-tag">推荐</span>
                  </div>
                  <span class="quality-desc">DASH 高清 · FFmpeg合成</span>
                </div>
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === '720p' }"
                  @click="selectedQuality = '720p'"
                >
                  <div class="quality-top-row">
                    <span class="quality-label">720P 高清</span>
                  </div>
                  <span class="quality-desc">标准画质 · 极速下载</span>
                </div>
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === '480p' }"
                  @click="selectedQuality = '480p'"
                >
                  <div class="quality-top-row">
                    <span class="quality-label">480P 标清</span>
                  </div>
                  <span class="quality-desc">流媒体标清 · 极小体积</span>
                </div>
              </div>
            </div>

            <!-- Video Container Format -->
            <div class="option-row">
              <label class="label-heading">视频封装格式</label>
              <div class="format-pill-group">
                <button 
                  type="button"
                  class="format-pill-btn"
                  :class="{ 'active': selectedVideoFormat === 'mp4' }"
                  @click="selectedVideoFormat = 'mp4'"
                >
                  <span class="pill-title">MP4 格式</span>
                  <span class="pill-desc">(通用推荐 · 兼容手机/电脑/车载)</span>
                </button>
                <button 
                  type="button"
                  class="format-pill-btn"
                  :class="{ 'active': selectedVideoFormat === 'mkv' }"
                  @click="selectedVideoFormat = 'mkv'"
                >
                  <span class="pill-title">MKV 容器</span>
                  <span class="pill-desc">(高清无损 · 支持多音轨封包)</span>
                </button>
              </div>
            </div>

            <button class="btn btn-secondary start-btn download-btn" @click="startVideoDownload">
              <span>立即下载视频文件 (.{{ selectedVideoFormat }})</span>
            </button>
          </div>
        </div>

        <!-- Pathway 3: Extract Audio Only -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'audio' }">
          <div class="choice-header" @click="activeChoice = 'audio'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'audio' }"></span>
            </div>
            <div class="choice-info">
              <div class="choice-title-row">
                <h4>🎵 纯音频提取与下载 (Extract Audio)</h4>
                <span class="choice-badge music">音乐/播客专用</span>
              </div>
              <p>仅提取音频流并自动转码，无需下载臃肿视频，支持高保真 MP3、苹果 M4A 以及母带级无损格式。</p>
            </div>
          </div>

          <!-- Drawer 3: Audio Extraction Options -->
          <div class="choice-drawer" v-if="activeChoice === 'audio'">
            <div class="option-row">
              <label class="label-heading">目标音频格式</label>
              <div class="audio-format-grid">
                <div 
                  class="audio-card" 
                  :class="{ 'active': selectedAudioFormat === 'mp3' }"
                  @click="selectedAudioFormat = 'mp3'"
                >
                  <div class="audio-header">
                    <span class="audio-title">MP3 格式</span>
                    <span class="audio-rate">320 kbps</span>
                  </div>
                  <span class="audio-desc">全设备车载通用 · 极高音质</span>
                </div>

                <div 
                  class="audio-card" 
                  :class="{ 'active': selectedAudioFormat === 'm4a' }"
                  @click="selectedAudioFormat = 'm4a'"
                >
                  <div class="audio-header">
                    <span class="audio-title">M4A 格式</span>
                    <span class="audio-rate">AAC 原生</span>
                  </div>
                  <span class="audio-desc">苹果与移动生态 · 高保真音质</span>
                </div>

                <div 
                  class="audio-card" 
                  :class="{ 'active': selectedAudioFormat === 'wav' }"
                  @click="selectedAudioFormat = 'wav'"
                >
                  <div class="audio-header">
                    <span class="audio-title">WAV 格式</span>
                    <span class="audio-rate">16-bit PCM</span>
                  </div>
                  <span class="audio-desc">无损母带 · 原始未压缩音频</span>
                </div>

                <div 
                  class="audio-card" 
                  :class="{ 'active': selectedAudioFormat === 'flac' }"
                  @click="selectedAudioFormat = 'flac'"
                >
                  <div class="audio-header">
                    <span class="audio-title">FLAC 格式</span>
                    <span class="audio-rate">Hi-Res 无损</span>
                  </div>
                  <span class="audio-desc">发烧级高解析 · 纯净听感</span>
                </div>
              </div>
            </div>

            <button class="btn btn-secondary start-btn audio-btn" @click="startAudioDownload">
              <span>立即提取并下载音频 (.{{ selectedAudioFormat }})</span>
            </button>
          </div>
        </div>

        <!-- Pathway 4: Smart Subtitle Burn (Hardcoding) -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'burn' }">
          <div class="choice-header" @click="activeChoice = 'burn'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'burn' }"></span>
            </div>
            <div class="choice-info">
              <div class="choice-title-row">
                <h4>⚡ 智能字幕压制 (Smart Subtitle Hardcoding)</h4>
                <span class="choice-badge burn">一键全自动</span>
              </div>
              <p>全自动流水线：下载视频 ➔ Whisper 语音识别 ➔ 生成特效字幕 ➔ FFmpeg 硬件级内嵌压制。</p>
            </div>
          </div>

          <!-- Drawer 4: Subtitle Hardcoding Options -->
          <div class="choice-drawer" v-if="activeChoice === 'burn'">
            <div class="burn-settings-grid">
              <!-- Translation mode -->
              <div class="setting-block">
                <label class="setting-title">字幕翻译模式</label>
                <div class="btn-group-segmented">
                  <button 
                    type="button"
                    class="seg-btn" 
                    :class="{ active: !burnTranslate }"
                    @click="burnTranslate = false"
                  >
                    保持原声字幕
                  </button>
                  <button 
                    type="button"
                    class="seg-btn" 
                    :class="{ active: burnTranslate }"
                    @click="burnTranslate = true"
                  >
                    智能英译中 (中文字幕)
                  </button>
                </div>
              </div>

              <!-- Subtitle style preset -->
              <div class="setting-block">
                <label class="setting-title">字幕视觉样式</label>
                <div class="btn-group-segmented">
                  <button 
                    type="button"
                    class="seg-btn" 
                    :class="{ active: burnStylePreset === 'box' }"
                    @click="burnStylePreset = 'box'"
                  >
                    经典黑金底框
                  </button>
                  <button 
                    type="button"
                    class="seg-btn" 
                    :class="{ active: burnStylePreset === 'outline' }"
                    @click="burnStylePreset = 'outline'"
                  >
                    白字微光投影
                  </button>
                  <button 
                    type="button"
                    class="seg-btn" 
                    :class="{ active: burnStylePreset === 'cyan' }"
                    @click="burnStylePreset = 'cyan'"
                  >
                    赛博青霓虹
                  </button>
                </div>
              </div>

              <!-- Font size & Position -->
              <div class="setting-block-inline">
                <div class="setting-sub-col">
                  <label class="setting-title">字幕字号</label>
                  <select v-model.number="burnFontSize" class="custom-select">
                    <option :value="18">小号 (18px)</option>
                    <option :value="22">推荐标准 (22px)</option>
                    <option :value="26">大号醒目 (26px)</option>
                  </select>
                </div>

                <div class="setting-sub-col">
                  <label class="setting-title">显示位置</label>
                  <select v-model="burnPosition" class="custom-select">
                    <option value="bottom">底部居中 (推荐)</option>
                    <option value="top">顶部居中</option>
                  </select>
                </div>
              </div>
            </div>

            <button class="btn btn-primary start-btn burn-btn" @click="startSubtitleBurn">
              <span>⚡ 开始一键全自动压制字幕 (.mp4)</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Video / Audio Download & Burning Progress Tracking -->
      <div class="download-progress-container glass-panel animate-fade-in" v-if="isDownloading">
        <div class="dl-progress-header">
          <div class="dl-status-title-box">
            <span class="dl-loader-icon"></span>
            <h4>{{ downloadStatusText }}</h4>
          </div>
          <div class="dl-header-actions">
            <span class="dl-percentage">{{ Math.round(downloadProgress) }}%</span>
            <button class="cancel-task-btn" @click="cancelDownload" title="取消当前任务">取消</button>
          </div>
        </div>
        
        <div class="progress-track">
          <div class="progress-bar-fill platform-fill" :style="{ width: downloadProgress + '%' }"></div>
        </div>

        <div class="dl-progress-hints">
          提示：{{ downloadHintText || '后台工作进程正在执行分轨下载与本地 FFmpeg 流水线封装，完成后将自动触发保存。' }}
        </div>
      </div>
    </div>

    <!-- Active Transcribing Overlay -->
    <div class="progress-overlay" v-if="isTranscribing">
      <div class="progress-card glass-panel">
        <h3>{{ progressStatus }}</h3>
        <p class="progress-details">{{ progressDetails }}</p>
        <div class="progress-track">
          <div class="progress-bar-fill" :style="{ width: progressPercentage + '%' }"></div>
        </div>
        <div class="progress-percentage-label">{{ progressPercentage }}%</div>
        <div class="wave-visualizer" v-if="progressPercentage > 0 && progressPercentage < 100">
          <div class="wave-bar" v-for="i in 10" :key="i" :style="{ animationDelay: (i * 0.1) + 's' }"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';

const props = defineProps({
  isTranscribing: {
    type: Boolean,
    default: false
  },
  progressStatus: {
    type: String,
    default: '准备中...'
  },
  progressDetails: {
    type: String,
    default: '正在解析链接...'
  },
  progressPercentage: {
    type: Number,
    default: 0
  }
});

const emit = defineEmits(['start-transcription']);

const onlineUrl = ref('');
const isLoadingInfo = ref(false);
const videoInfo = ref(null);
const fetchError = ref('');
const copiedUrl = ref(false);

// Active pathway choice
const activeChoice = ref('transcribe'); // 'transcribe' | 'download' | 'audio' | 'burn'

// Pathway 1: Transcribe
const selectedModel = ref('base');

// Pathway 2: Download Video
const selectedQuality = ref('1080p'); // 'best' | '1080p' | '720p' | '480p'
const selectedVideoFormat = ref('mp4'); // 'mp4' | 'mkv'

// Pathway 3: Extract Audio
const selectedAudioFormat = ref('mp3'); // 'mp3' | 'm4a' | 'wav' | 'flac'

// Pathway 4: Burn Subtitles
const burnTranslate = ref(false);
const burnStylePreset = ref('box'); // 'box' | 'outline' | 'cyan'
const burnFontSize = ref(22);
const burnPosition = ref('bottom');

// Image loading error fallbacks
const coverError = ref(false);
const avatarError = ref(false);

// Background Task Progress Tracking
const isDownloading = ref(false);
const downloadProgress = ref(0);
const downloadStatusText = ref('初始化任务中...');
const downloadHintText = ref('');
let pollingInterval = null;

const models = [
  { value: 'tiny', name: 'Tiny (极速)', size: '75 MB', desc: '极快识别，轻量占用' },
  { value: 'base', name: 'Base (推荐)', size: '140 MB', desc: '速度准确度平衡佳' },
  { value: 'small', name: 'Small (高精)', size: '460 MB', desc: '中文与长句高精准' },
  { value: 'medium', name: 'Medium (专家)', size: '1.5 GB', desc: '专业级多语言精听' }
];

// Helper: Proxies image URLs through backend to bypass firewall / proxy blocks
const getProxiedImageUrl = (url) => {
  if (!url) return '';
  return `/api/transcription/online/image-proxy?url=${encodeURIComponent(url)}`;
};

// Detect platform from user input URL in real-time
const detectedPlatform = computed(() => {
  const url = onlineUrl.value.toLowerCase().trim();
  if (url.includes('bilibili.com') || url.includes('b23.tv')) {
    return 'bilibili';
  }
  return 'youtube';
});

const themeColor = computed(() => {
  if (videoInfo.value) {
    return videoInfo.value.platform === 'bilibili' ? '#fb7299' : '#ef4444';
  }
  return detectedPlatform.value === 'bilibili' ? '#fb7299' : '#ef4444';
});

let typingTimeout = null;
const handleUrlInput = () => {
  clearTimeout(typingTimeout);
  fetchError.value = '';
  
  const url = onlineUrl.value.trim();
  if (!url) return;

  // Trigger preview fetch if it looks like a valid online video link
  if (url.includes('youtube.com/') || url.includes('youtu.be/') || url.includes('bilibili.com/') || url.includes('b23.tv/')) {
    if (!url.includes(' ') && !url.includes('\n')) {
      typingTimeout = setTimeout(fetchVideoInfo, 750);
    }
  }
};

const fetchVideoInfo = async () => {
  const url = onlineUrl.value.trim();
  if (!url) return;

  isLoadingInfo.value = true;
  fetchError.value = '';
  videoInfo.value = null;
  coverError.value = false;
  avatarError.value = false;

  try {
    const response = await fetch(`/api/transcription/online/info?url=${encodeURIComponent(url)}`);
    if (!response.ok) {
      const errText = await response.text();
      throw new Error(errText || `HTTP status ${response.status}`);
    }
    const data = await response.json();
    videoInfo.value = data;
  } catch (error) {
    console.error('Failed to fetch video info:', error);
    fetchError.value = error.message || '网络连接超时';
  } finally {
    isLoadingInfo.value = false;
  }
};

// 1. Start Transcription
const startTranscription = () => {
  if (!videoInfo.value) return;
  emit('start-transcription', {
    url: videoInfo.value.url,
    modelType: selectedModel.value
  });
};

// 2. Start Video Download
const startVideoDownload = async () => {
  if (!videoInfo.value) return;
  
  isDownloading.value = true;
  downloadProgress.value = 0;
  downloadStatusText.value = '正在解析视频画质分轨...';
  downloadHintText.value = `系统正在获取 ${selectedQuality.value.toUpperCase()} 轨道媒体流，并调用本地 FFmpeg 封装为 .${selectedVideoFormat.value} 文件。`;

  try {
    const startResponse = await fetch('/api/transcription/online/download/start', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        url: videoInfo.value.url,
        quality: selectedQuality.value,
        downloadType: 'video',
        videoFormat: selectedVideoFormat.value
      })
    });

    if (!startResponse.ok) {
      const errText = await startResponse.text();
      throw new Error(errText || '无法启动视频下载任务');
    }

    const { taskId } = await startResponse.json();
    pollTaskStatus(taskId, `${videoInfo.value.title || 'video'}.${selectedVideoFormat.value}`);
  } catch (error) {
    alert('请求视频下载出错: ' + error.message);
    isDownloading.value = false;
  }
};

// 3. Start Audio Extraction
const startAudioDownload = async () => {
  if (!videoInfo.value) return;
  
  isDownloading.value = true;
  downloadProgress.value = 0;
  downloadStatusText.value = `正在提取音频轨并转码为 ${selectedAudioFormat.value.toUpperCase()}...`;
  downloadHintText.value = `系统仅下载音频流，通过本地 FFmpeg 高音质编码为 .${selectedAudioFormat.value}，无需下载庞大视频文件。`;

  try {
    const startResponse = await fetch('/api/transcription/online/download/start', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        url: videoInfo.value.url,
        downloadType: 'audio',
        audioFormat: selectedAudioFormat.value
      })
    });

    if (!startResponse.ok) {
      const errText = await startResponse.text();
      throw new Error(errText || '无法启动音频提取任务');
    }

    const { taskId } = await startResponse.json();
    pollTaskStatus(taskId, `${videoInfo.value.title || 'audio'}.${selectedAudioFormat.value}`);
  } catch (error) {
    alert('请求音频提取出错: ' + error.message);
    isDownloading.value = false;
  }
};

// 4. Start Subtitle Burn
const startSubtitleBurn = async () => {
  if (!videoInfo.value) return;
  
  isDownloading.value = true;
  downloadProgress.value = 0;
  downloadStatusText.value = '正在准备智能字幕压制流水线...';
  downloadHintText.value = '全自动流水线：视频下载 ➔ 音轨提取 ➔ Whisper 语音识别 ➔ ASS 特效字幕生成 ➔ FFmpeg 硬件级烧录。';

  let fontColor = 'ffffff';
  let subtitleStyle = 'outline';
  if (burnStylePreset.value === 'box') {
    fontColor = 'ffff00';
    subtitleStyle = 'box';
  } else if (burnStylePreset.value === 'cyan') {
    fontColor = '00ffff';
    subtitleStyle = 'outline';
  } else {
    fontColor = 'ffffff';
    subtitleStyle = 'outline';
  }

  try {
    const startResponse = await fetch('/api/transcription/video/burn-subtitles', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        videoUrl: videoInfo.value.url,
        title: videoInfo.value.title,
        modelType: selectedModel.value,
        translateToChinese: burnTranslate.value,
        fontSize: burnFontSize.value,
        fontColor: fontColor,
        subtitleStyle: subtitleStyle,
        position: burnPosition.value
      })
    });

    if (!startResponse.ok) {
      const errText = await startResponse.text();
      throw new Error(errText || '无法启动字幕压制任务');
    }

    const { taskId } = await startResponse.json();
    pollTaskStatus(taskId, `${videoInfo.value.title || 'video'}_subtitled.mp4`);
  } catch (error) {
    alert('请求字幕压制出错: ' + error.message);
    isDownloading.value = false;
  }
};

// Polling Helper
const pollTaskStatus = (taskId, defaultFilename) => {
  clearInterval(pollingInterval);
  pollingInterval = setInterval(async () => {
    try {
      const statusResponse = await fetch(`/api/transcription/youtube/download/status?taskId=${encodeURIComponent(taskId)}`);
      if (!statusResponse.ok) return;

      const data = await statusResponse.json();
      
      downloadProgress.value = data.progress;
      downloadStatusText.value = data.statusText;

      if (data.isComplete) {
        clearInterval(pollingInterval);
        downloadProgress.value = 100;
        downloadStatusText.value = '处理全部完成！正在自动保存到本地...';

        setTimeout(() => {
          const finalUrl = `/api/transcription/youtube/download/file?taskId=${encodeURIComponent(taskId)}`;
          const a = document.createElement('a');
          a.href = finalUrl;
          a.setAttribute('download', defaultFilename || '');
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);

          isDownloading.value = false;
        }, 1000);
      } else if (data.error) {
        clearInterval(pollingInterval);
        alert('处理失败: ' + data.error);
        isDownloading.value = false;
      }
    } catch (err) {
      console.error('Polling status error:', err);
    }
  }, 850);
};

const cancelDownload = () => {
  if (confirm('确定要取消当前后台任务吗？')) {
    clearInterval(pollingInterval);
    isDownloading.value = false;
  }
};

const clearAll = () => {
  clearInterval(pollingInterval);
  onlineUrl.value = '';
  videoInfo.value = null;
  fetchError.value = '';
  isDownloading.value = false;
  coverError.value = false;
  avatarError.value = false;
};

const copyVideoUrl = () => {
  if (!videoInfo.value || !videoInfo.value.url) return;
  navigator.clipboard.writeText(videoInfo.value.url).then(() => {
    copiedUrl.value = true;
    setTimeout(() => { copiedUrl.value = false; }, 2000);
  }).catch(() => {
    alert('已选视频链接: ' + videoInfo.value.url);
  });
};

const downloadCover = () => {
  if (!videoInfo.value || !videoInfo.value.thumbnailUrl) return;
  const coverUrl = `/api/transcription/online/download-cover?url=${encodeURIComponent(videoInfo.value.thumbnailUrl)}`;
  const a = document.createElement('a');
  a.href = coverUrl;
  a.setAttribute('download', 'cover.jpg');
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
};

const formatDuration = (secs) => {
  if (isNaN(secs) || secs <= 0) return '00:00';
  const h = Math.floor(secs / 3600);
  const m = Math.floor((secs % 3600) / 60);
  const s = Math.floor(secs % 60);
  
  if (h > 0) {
    return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
};
</script>

<style scoped>
.online-input-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
  position: relative;
  min-height: 200px;
  transition: all 0.3s ease;
}

.input-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.url-input-wrapper {
  display: flex;
  align-items: center;
  background: rgba(8, 11, 17, 0.5);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 6px 14px;
  position: relative;
  transition: all 0.3s ease;
}

.url-input-wrapper:focus-within {
  border-color: var(--platform-color);
  box-shadow: 0 0 10px rgba(var(--platform-color), 0.15);
}

.input-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  flex-shrink: 0;
}

.platform-svg-icon {
  width: 24px;
  height: 24px;
  transition: color 0.3s ease;
}

.yt-icon {
  color: #ef4444;
}

.bili-icon {
  color: #fb7299;
}

.animate-pop {
  animation: popEffect 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275) forwards;
}

@keyframes popEffect {
  0% { transform: scale(0.6); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}

.url-input-box {
  background: transparent;
  border: none;
  outline: none;
  color: var(--text-primary);
  font-size: 0.95rem;
  width: 100%;
  padding: 8px 0;
}

.clear-input-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 1.25rem;
  padding: 0 8px;
  transition: color 0.2s ease;
}

.clear-input-btn:hover {
  color: var(--text-primary);
}

.mini-spinner {
  width: 18px;
  height: 18px;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-top-color: var(--platform-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin-right: 8px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.input-hint {
  font-size: 0.75rem;
  color: var(--text-muted);
  padding-left: 4px;
}

.fetch-error-box {
  display: flex;
  align-items: center;
  gap: 8px;
  background: rgba(239, 68, 68, 0.1);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: var(--border-radius-md);
  padding: 12px;
  color: #f87171;
  font-size: 0.8rem;
  margin-top: 12px;
  line-height: 1.4;
}

.error-svg {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}

/* Video Preview Flow */
.video-preview-flow {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.top-nav-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.back-link-btn {
  background: transparent;
  border: none;
  color: var(--text-secondary);
  font-size: 0.85rem;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.back-link-btn:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
}

.mini-back-icon {
  width: 14px;
  height: 14px;
}

.source-platform-pill {
  font-size: 0.7rem;
  padding: 2px 8px;
  border-radius: 12px;
  font-weight: 500;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-color);
  color: var(--text-muted);
}

.source-platform-pill.bilibili {
  border-color: rgba(251, 114, 153, 0.3);
  color: #fb7299;
  background: rgba(251, 114, 153, 0.08);
}

.source-platform-pill.youtube {
  border-color: rgba(239, 68, 68, 0.3);
  color: #ef4444;
  background: rgba(239, 68, 68, 0.08);
}

/* Video info card */
.video-info-card {
  display: flex;
  gap: 16px;
  background: rgba(8, 11, 17, 0.5);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 14px;
  flex-wrap: wrap;
}

.video-cover-wrapper {
  position: relative;
  width: 140px;
  height: 84px;
  border-radius: 8px;
  overflow: hidden;
  flex-shrink: 0;
  background: #0f172a;
}

.video-cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.3s ease;
}

.video-cover:hover {
  transform: scale(1.05);
}

.video-cover-fallback {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
  color: var(--text-muted);
}

.video-cover-fallback.bilibili {
  background: linear-gradient(135deg, rgba(251, 114, 153, 0.25) 0%, #0f172a 100%);
}

.video-cover-fallback.youtube {
  background: linear-gradient(135deg, rgba(239, 68, 68, 0.25) 0%, #0f172a 100%);
}

.fallback-cover-icon {
  width: 24px;
  height: 24px;
  opacity: 0.8;
}

.fallback-text {
  font-size: 0.65rem;
  color: var(--text-secondary);
}

.video-duration-tag {
  position: absolute;
  bottom: 4px;
  right: 4px;
  background: rgba(0, 0, 0, 0.85);
  font-family: var(--font-mono);
  font-size: 0.65rem;
  padding: 2px 5px;
  border-radius: 3px;
  color: white;
}

.platform-badge-tag {
  position: absolute;
  top: 4px;
  left: 4px;
  font-size: 0.6rem;
  font-weight: 600;
  padding: 1px 5px;
  border-radius: 3px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.platform-badge-tag.youtube {
  background: #ef4444;
  color: white;
}

.platform-badge-tag.bilibili {
  background: #fb7299;
  color: white;
}

.video-details-wrapper {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  flex-grow: 1;
  min-width: 200px;
}

.video-preview-title {
  font-size: 0.95rem;
  line-height: 1.45;
  color: var(--text-primary);
  font-weight: 600;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 8px;
}

.creator-profile-box {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.creator-info-inline {
  display: flex;
  align-items: center;
  gap: 8px;
}

.creator-avatar {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  object-fit: cover;
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.creator-avatar-fallback {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: #334155;
  color: white;
  font-size: 0.7rem;
  font-weight: bold;
  display: flex;
  align-items: center;
  justify-content: center;
}

.creator-avatar-fallback.bilibili {
  background: #fb7299;
}

.creator-avatar-fallback.youtube {
  background: #ef4444;
}

.creator-name {
  font-size: 0.78rem;
  color: var(--text-secondary);
  font-weight: 500;
  max-width: 140px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.cover-actions-group {
  display: flex;
  align-items: center;
  gap: 6px;
}

.download-cover-btn {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  padding: 4px 8px;
  font-size: 0.7rem;
  color: var(--text-secondary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s ease;
}

.download-cover-btn:hover {
  background: var(--platform-color);
  border-color: var(--platform-color);
  color: white;
}

.cover-dl-icon {
  width: 12px;
  height: 12px;
}

/* Choices Selection Container */
.choices-container {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.choice-card {
  padding: 14px 16px;
  border-radius: var(--border-radius-md);
  cursor: pointer;
  transition: all 0.25s ease;
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: rgba(15, 21, 36, 0.4) !important;
  border: 1px solid var(--border-color);
}

.choice-card:hover {
  border-color: rgba(255, 255, 255, 0.12);
  background: rgba(15, 21, 36, 0.6) !important;
}

.choice-card.focused {
  border-color: var(--platform-color) !important;
  background: rgba(255, 255, 255, 0.02) !important;
  box-shadow: 0 4px 18px rgba(0, 0, 0, 0.25);
}

.choice-header {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.choice-radio {
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: 2px solid var(--text-muted);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: 2px;
  flex-shrink: 0;
  transition: all 0.2s ease;
}

.choice-card.focused .choice-radio {
  border-color: var(--platform-color);
}

.radio-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: transparent;
  transition: all 0.2s ease;
}

.radio-dot.active {
  background: var(--platform-color);
}

.choice-info {
  flex-grow: 1;
}

.choice-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.choice-info h4 {
  font-size: 0.92rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.choice-badge {
  font-size: 0.65rem;
  padding: 1px 6px;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.08);
  color: var(--text-secondary);
}

.choice-badge.music {
  background: rgba(168, 85, 247, 0.15);
  color: #c084fc;
}

.choice-badge.burn {
  background: rgba(234, 179, 8, 0.15);
  color: #facc15;
}

.choice-info p {
  font-size: 0.75rem;
  color: var(--text-muted);
  line-height: 1.45;
  margin: 0;
}

/* Choice Drawers */
.choice-drawer {
  border-top: 1px solid var(--border-color);
  padding-top: 14px;
  animation: slideDown 0.3s ease forwards;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-5px); }
  to { opacity: 1; transform: translateY(0); }
}

.option-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.option-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.label-heading {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-secondary);
}

.option-hint-text {
  font-size: 0.68rem;
  color: var(--text-muted);
}

/* Models Grid (4 items) */
.model-grid-compact {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
}

@media (max-width: 640px) {
  .model-grid-compact {
    grid-template-columns: repeat(2, 1fr);
  }
}

.model-card-compact {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 8px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: all 0.2s ease;
}

.model-card-compact:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.model-card-compact.active {
  border-color: var(--platform-color);
  background: rgba(255, 255, 255, 0.04);
}

.model-header-compact {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.model-name-compact {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--text-primary);
}

.model-size-compact {
  font-size: 0.65rem;
  color: var(--text-muted);
  font-family: var(--font-mono);
}

.model-desc-text {
  font-size: 0.65rem;
  color: var(--text-muted);
  line-height: 1.25;
}

/* Quality Selection Styles */
.quality-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px;
}

.quality-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 10px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: all 0.2s ease;
}

.quality-card:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.quality-card.active {
  border-color: var(--platform-color);
  background: rgba(255, 255, 255, 0.03);
}

.quality-top-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.quality-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-primary);
}

.mini-tag {
  font-size: 0.6rem;
  padding: 1px 4px;
  border-radius: 3px;
  background: rgba(255, 255, 255, 0.1);
  color: var(--text-secondary);
}

.mini-tag.gold {
  background: rgba(234, 179, 8, 0.15);
  color: #facc15;
}

.quality-desc {
  font-size: 0.65rem;
  color: var(--text-muted);
}

/* Video format pill buttons */
.format-pill-group {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.format-pill-btn {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 8px 12px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  transition: all 0.2s ease;
  color: var(--text-secondary);
}

.format-pill-btn:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.format-pill-btn.active {
  border-color: var(--platform-color);
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-primary);
}

.pill-title {
  font-size: 0.8rem;
  font-weight: 600;
}

.pill-desc {
  font-size: 0.65rem;
  color: var(--text-muted);
}

/* Audio Format Grid (4 items) */
.audio-format-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px;
}

.audio-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 10px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: all 0.2s ease;
}

.audio-card:hover {
  border-color: rgba(255, 255, 255, 0.15);
}

.audio-card.active {
  border-color: #a855f7;
  background: rgba(168, 85, 247, 0.06);
}

.audio-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.audio-title {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-primary);
}

.audio-rate {
  font-family: var(--font-mono);
  font-size: 0.65rem;
  color: #c084fc;
}

.audio-desc {
  font-size: 0.65rem;
  color: var(--text-muted);
}

/* Subtitle Burning Settings Grid */
.burn-settings-grid {
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: rgba(0, 0, 0, 0.2);
  padding: 12px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.setting-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.setting-block-inline {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.setting-title {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--text-secondary);
}

.btn-group-segmented {
  display: flex;
  background: rgba(0, 0, 0, 0.4);
  border-radius: 6px;
  padding: 3px;
  gap: 4px;
  border: 1px solid var(--border-color);
}

.seg-btn {
  flex: 1;
  background: transparent;
  border: none;
  border-radius: 4px;
  padding: 6px 10px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.2s ease;
  white-space: nowrap;
}

.seg-btn:hover {
  color: var(--text-primary);
}

.seg-btn.active {
  background: var(--platform-color);
  color: white;
  font-weight: 600;
}

.custom-select {
  background: rgba(8, 11, 17, 0.7);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  color: var(--text-primary);
  padding: 6px 8px;
  font-size: 0.75rem;
  outline: none;
  cursor: pointer;
}

.custom-select:focus {
  border-color: var(--platform-color);
}

/* Action Buttons */
.start-btn {
  width: 100%;
  padding: 10px;
  font-size: 0.85rem;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  transition: all 0.2s ease;
}

.btn-primary {
  background: linear-gradient(135deg, var(--platform-color) 0%, #1e293b 120%);
  color: white;
  border: none;
}

.btn-primary:hover {
  opacity: 0.92;
  transform: translateY(-1px);
}

.download-btn {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
  border: 1px solid var(--border-color);
}

.download-btn:hover:not(:disabled) {
  background: var(--platform-color);
  border-color: var(--platform-color);
  color: white;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.25);
}

.audio-btn {
  background: rgba(168, 85, 247, 0.12);
  border: 1px solid rgba(168, 85, 247, 0.3);
  color: #e9d5ff;
}

.audio-btn:hover:not(:disabled) {
  background: #a855f7;
  border-color: #a855f7;
  color: white;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(168, 85, 247, 0.3);
}

.burn-btn {
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  color: white;
  border: none;
}

.burn-btn:hover {
  opacity: 0.95;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(245, 158, 11, 0.3);
}

/* Download progress bar container styles */
.download-progress-container {
  padding: 16px 20px;
  background: rgba(15, 21, 36, 0.7);
  border: 1px solid var(--border-color);
  box-shadow: 0 8px 30px rgba(0,0,0,0.3);
  border-radius: var(--border-radius-md);
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.dl-progress-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.dl-status-title-box {
  display: flex;
  align-items: center;
  gap: 10px;
}

.dl-loader-icon {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-top-color: var(--platform-color);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.dl-status-title-box h4 {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0;
}

.dl-header-actions {
  display: flex;
  align-items: center;
  gap: 12px;
}

.dl-percentage {
  font-family: var(--font-mono);
  font-size: 1rem;
  font-weight: 700;
  color: var(--platform-color);
}

.cancel-task-btn {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-color);
  color: var(--text-muted);
  border-radius: 4px;
  font-size: 0.7rem;
  padding: 2px 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.cancel-task-btn:hover {
  background: rgba(239, 68, 68, 0.2);
  border-color: #ef4444;
  color: #f87171;
}

.platform-fill {
  background: var(--platform-color) !important;
  box-shadow: 0 0 10px var(--platform-color) !important;
}

.dl-progress-hints {
  font-size: 0.72rem;
  color: var(--text-muted);
  line-height: 1.45;
  border-top: 1px dashed var(--border-color);
  padding-top: 8px;
}

/* Progress Overlay */
.progress-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(8, 11, 17, 0.85);
  backdrop-filter: blur(8px);
  z-index: 10;
  border-radius: var(--border-radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

.progress-card {
  width: 100%;
  max-width: 480px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  background: var(--bg-secondary) !important;
}

.progress-card h3 {
  font-size: 1.25rem;
  margin-bottom: 8px;
  background: linear-gradient(135deg, var(--platform-color) 0%, #ffffff 150%);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
}

.progress-details {
  font-size: 0.875rem;
  color: var(--text-secondary);
  margin-bottom: 24px;
  min-height: 20px;
}

.progress-track {
  width: 100%;
  height: 6px;
  background: var(--bg-tertiary);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 12px;
}

.progress-bar-fill {
  height: 100%;
  background: linear-gradient(to right, var(--platform-color), #ffffff);
  box-shadow: 0 0 10px var(--platform-color);
  border-radius: 3px;
  transition: width 0.4s ease;
}

.progress-percentage-label {
  font-family: var(--font-mono);
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--platform-color);
  margin-bottom: 16px;
}

.wave-visualizer {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  height: 32px;
}

.wave-bar {
  width: 3px;
  height: 8px;
  background: var(--platform-color);
  border-radius: 1.5px;
  animation: wave 1.2s ease-in-out infinite alternate;
}

@keyframes wave {
  0% {
    height: 8px;
    background: var(--platform-color);
  }
  100% {
    height: 32px;
    background: #ffffff;
  }
}
</style>