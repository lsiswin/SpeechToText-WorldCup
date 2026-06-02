<template>
  <div class="glass-panel youtube-input-container">
    <!-- Input Section -->
    <div class="input-section" v-if="!videoInfo">
      <div class="url-input-wrapper">
        <div class="input-icon">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="yt-svg-icon">
            <path d="M23.498 6.163a3.003 3.003 0 0 0-2.11-2.108C19.524 3.545 12 3.545 12 3.545s-7.525 0-9.388.51a3.002 3.002 0 0 0-2.11 2.108C0 8.029 0 12 0 12s0 3.972.502 5.837a3.003 3.003 0 0 0 2.11 2.108c1.863.51 9.388.51 9.388.51s7.525 0 9.388-.51a3.002 3.002 0 0 0 2.11-2.108C24 15.972 24 12 24 12s0-3.973-.502-5.837zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/>
          </svg>
        </div>
        <input 
          type="text" 
          v-model="youtubeUrl" 
          placeholder="请输入 YouTube 视频链接 进行预览..."
          class="url-input-box"
          :disabled="isLoadingInfo || isTranscribing"
          @input="handleUrlInput"
        />
        <button 
          v-if="youtubeUrl && !isLoadingInfo && !isTranscribing" 
          class="clear-input-btn" 
          @click="clearAll"
        >×</button>
        <div v-if="isLoadingInfo" class="mini-spinner"></div>
      </div>
      <p class="input-hint">粘贴链接后，系统会自动拉取视频封面与标题信息</p>
      
      <!-- Fetch Error Alert -->
      <div v-if="fetchError" class="fetch-error-box">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="error-svg">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>
        <span>视频解析失败: {{ fetchError }} (请检查网络或代理配置)</span>
      </div>
    </div>

    <!-- Active Video Card & Actions Selection -->
    <div v-else class="video-preview-flow animate-fade-in">
      <!-- Back Button -->
      <button class="back-link-btn" @click="clearAll" :disabled="isTranscribing || isDownloading">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-back-icon">
          <path fill-rule="evenodd" d="M9.707 16.707a1 1 0 01-1.414 0l-6-6a1 1 0 010-1.414l6-6a1 1 0 011.414 1.414L5.414 9H17a1 1 0 110 2H5.414l4.293 4.293a1 1 0 010 1.414z" clip-rule="evenodd" />
        </svg>
        返回输入链接
      </button>

      <!-- Video Preview Card (Cover / Title) -->
      <div class="video-info-card">
        <div class="video-cover-wrapper">
          <img :src="videoInfo.thumbnailUrl" class="video-cover" alt="Video Thumbnail" referrerpolicy="no-referrer" />
          <span class="video-duration-tag">{{ formatDuration(videoInfo.durationSeconds) }}</span>
        </div>
        <div class="video-details-wrapper">
          <h3 class="video-preview-title">{{ videoInfo.title }}</h3>
          
          <div class="creator-profile-box">
            <img v-if="videoInfo.avatarUrl" :src="videoInfo.avatarUrl" class="creator-avatar" referrerpolicy="no-referrer" />
            <span class="creator-name">{{ videoInfo.author }}</span>
          </div>
        </div>
      </div>

      <!-- Action Choice Container -->
      <div class="choices-container" v-if="!isTranscribing && !isDownloading">
        <!-- Path A: Speech to Text Options -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'transcribe' }">
          <div class="choice-header" @click="activeChoice = 'transcribe'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'transcribe' }"></span>
            </div>
            <div class="choice-info">
              <h4>语音转写文字 (Speech to Text)</h4>
              <p>分析音轨，通过 Whisper 离线生成带时间轴的文本段落。</p>
            </div>
          </div>

          <!-- Options Drawer (opens if choice active) -->
          <div class="choice-drawer" v-if="activeChoice === 'transcribe'">
            <div class="option-row">
              <label class="label-heading">Whisper 识别模型</label>
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
                </div>
              </div>
            </div>
            
            <button class="btn btn-primary start-btn" @click="startTranscription">
              开始语音识别
            </button>
          </div>
        </div>

        <!-- Path B: Download Video File -->
        <div class="choice-card glass-panel" :class="{ 'focused': activeChoice === 'download' }">
          <div class="choice-header" @click="activeChoice = 'download'">
            <div class="choice-radio">
              <span class="radio-dot" :class="{ 'active': activeChoice === 'download' }"></span>
            </div>
            <div class="choice-info">
              <h4>直接下载视频 (Download Video)</h4>
              <p>获取 MP4 视频媒体流，支持 1080P 高清拼接或 720P 极速混合流。</p>
            </div>
          </div>

          <div class="choice-drawer" v-if="activeChoice === 'download'">
            <div class="option-row">
              <label class="label-heading">选择下载画质</label>
              <div class="quality-grid">
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === '720p' }"
                  @click="selectedQuality = '720p'"
                >
                  <span class="quality-label">720P 标清</span>
                  <span class="quality-desc">混合单轨 · 下载极速</span>
                </div>
                <div 
                  class="quality-card" 
                  :class="{ 'active': selectedQuality === '1080p' }"
                  @click="selectedQuality = '1080p'"
                >
                  <span class="quality-label">1080P 全高清</span>
                  <span class="quality-desc">独立双轨 · FFmpeg合成</span>
                </div>
              </div>
            </div>
            <button class="btn btn-secondary start-btn download-btn" @click="startDownload">
              <span>立即下载视频文件 (.mp4)</span>
            </button>
          </div>
        </div>
      </div>

      <!-- Video Download Progress Tracking (Only show when active downloading) -->
      <div class="download-progress-container glass-panel animate-fade-in" v-if="isDownloading">
        <div class="dl-progress-header">
          <div class="dl-status-title-box">
            <span class="dl-loader-icon"></span>
            <h4>{{ downloadStatusText }}</h4>
          </div>
          <span class="dl-percentage">{{ Math.round(downloadProgress) }}%</span>
        </div>
        <div class="progress-track">
          <div class="progress-bar-fill youtube-fill" :style="{ width: downloadProgress + '%' }"></div>
        </div>
        <div class="dl-progress-hints" v-if="selectedQuality === '1080p'">
          提示：1080P 将依次抓取视频与高音质音频，最后由本地 FFmpeg 自动拼接封包。
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

const youtubeUrl = ref('');
const isLoadingInfo = ref(false);
const videoInfo = ref(null);
const fetchError = ref('');

const activeChoice = ref('transcribe'); // 'transcribe' or 'download'
const selectedModel = ref('base');
const selectedQuality = ref('1080p'); // '720p' or '1080p'

// Download progress state
const isDownloading = ref(false);
const downloadProgress = ref(0);
const downloadStatusText = ref('初始化下载中...');
let pollingInterval = null;

const models = [
  { value: 'tiny', name: 'Tiny (极速)', size: '75 MB' },
  { value: 'base', name: 'Base (平衡)', size: '140 MB' },
  { value: 'small', name: 'Small (高精)', size: '460 MB' }
];

let typingTimeout = null;
const handleUrlInput = () => {
  clearTimeout(typingTimeout);
  fetchError.value = '';
  
  const url = youtubeUrl.value.trim();
  if (!url) return;

  if (url.includes('youtube.com/') || url.includes('youtu.be/')) {
    if (!url.includes(' ') && !url.includes('\n')) {
      typingTimeout = setTimeout(fetchVideoInfo, 800);
    }
  }
};

const fetchVideoInfo = async () => {
  const url = youtubeUrl.value.trim();
  if (!url) return;

  isLoadingInfo.value = true;
  fetchError.value = '';
  videoInfo.value = null;

  try {
    const response = await fetch(`/api/transcription/youtube/info?url=${encodeURIComponent(url)}`);
    if (!response.ok) {
      const errText = await response.text();
      throw new Error(errText || `HTTP status ${response.status}`);
    }
    const data = await response.json();
    videoInfo.value = data;
  } catch (error) {
    console.error('Failed to fetch YouTube info:', error);
    fetchError.value = error.message || '网络连接超时';
  } finally {
    isLoadingInfo.value = false;
  }
};

const startTranscription = () => {
  if (!videoInfo.value) return;
  emit('start-transcription', {
    url: videoInfo.value.url,
    modelType: selectedModel.value
  });
};

const startDownload = async () => {
  if (!videoInfo.value) return;
  
  isDownloading.value = true;
  downloadProgress.value = 0;
  downloadStatusText.value = '正在排队分配后台任务...';

  try {
    // 1. Send request to start asynchronous download task
    const startResponse = await fetch('/api/transcription/youtube/download/start', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        url: videoInfo.value.url,
        quality: selectedQuality.value
      })
    });

    if (!startResponse.ok) {
      const errText = await startResponse.text();
      throw new Error(errText || 'Failed to start download task.');
    }

    const { taskId } = await startResponse.json();
    
    // 2. Start polling for status
    pollingInterval = setInterval(async () => {
      try {
        const statusResponse = await fetch(`/api/transcription/youtube/download/status?taskId=${encodeURIComponent(taskId)}`);
        if (!statusResponse.ok) return;

        const data = await statusResponse.json();
        
        downloadProgress.value = data.progress;
        downloadStatusText.value = data.statusText;

        if (data.isComplete) {
          // Task completed! Stop polling and trigger browser file save
          clearInterval(pollingInterval);
          downloadProgress.value = 100;
          downloadStatusText.value = '合并打包成功！正在下载保存文件...';

          setTimeout(() => {
            const finalUrl = `/api/transcription/youtube/download/file?taskId=${encodeURIComponent(taskId)}`;
            const a = document.createElement('a');
            a.href = finalUrl;
            a.setAttribute('download', '');
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            // Complete download UX state
            isDownloading.value = false;
          }, 1000);
        } else if (data.error) {
          clearInterval(pollingInterval);
          alert('下载合并失败: ' + data.error);
          isDownloading.value = false;
        }
      } catch (err) {
        console.error('Polling status error:', err);
      }
    }, 850);

  } catch (error) {
    alert('请求下载出错: ' + error.message);
    isDownloading.value = false;
  }
};

const clearAll = () => {
  clearInterval(pollingInterval);
  youtubeUrl.value = '';
  videoInfo.value = null;
  fetchError.value = '';
  isDownloading.value = false;
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
.youtube-input-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
  position: relative;
  min-height: 200px;
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
  border-color: #ef4444;
  box-shadow: 0 0 10px rgba(239, 68, 68, 0.15);
}

.input-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 12px;
  flex-shrink: 0;
}

.yt-svg-icon {
  width: 24px;
  height: 24px;
  color: #ef4444;
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
  border-top-color: #ef4444;
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
  align-self: flex-start;
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

/* Video info card */
.video-info-card {
  display: flex;
  gap: 16px;
  background: rgba(8, 11, 17, 0.4);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 12px;
  flex-wrap: wrap;
}

.video-cover-wrapper {
  position: relative;
  width: 120px;
  height: 70px;
  border-radius: 6px;
  overflow: hidden;
  flex-shrink: 0;
}

.video-cover {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.video-duration-tag {
  position: absolute;
  bottom: 4px;
  right: 4px;
  background: rgba(0, 0, 0, 0.8);
  font-family: var(--font-mono);
  font-size: 0.65rem;
  padding: 2px 4px;
  border-radius: 2px;
  color: white;
}

.video-details-wrapper {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  flex-grow: 1;
  min-width: 180px;
}

.video-preview-title {
  font-size: 0.95rem;
  line-height: 1.4;
  color: var(--text-primary);
  font-weight: 600;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 6px;
}

.creator-profile-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.creator-avatar {
  width: 20px;
  height: 20px;
  border-radius: 50%;
}

.creator-name {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

/* Choices Selection Grid */
.choices-container {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.choice-card {
  padding: 16px;
  border-radius: var(--border-radius-md);
  cursor: pointer;
  transition: all 0.25s ease;
  display: flex;
  flex-direction: column;
  gap: 14px;
  background: rgba(15, 21, 36, 0.3) !important;
}

.choice-card:hover {
  border-color: rgba(255, 255, 255, 0.1);
  background: rgba(15, 21, 36, 0.5) !important;
}

.choice-card.focused {
  border-color: rgba(239, 68, 68, 0.25);
  background: rgba(239, 68, 68, 0.02) !important;
  box-shadow: 0 4px 15px rgba(239, 68, 68, 0.05);
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
  border-color: #ef4444;
}

.radio-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: transparent;
  transition: all 0.2s ease;
}

.radio-dot.active {
  background: #ef4444;
}

.choice-info h4 {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.choice-info p {
  font-size: 0.75rem;
  color: var(--text-muted);
  line-height: 1.4;
}

/* Drawer area inside choice cards */
.choice-drawer {
  border-top: 1px solid var(--border-color);
  padding-top: 14px;
  animation: slideDown 0.3s ease forwards;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

@keyframes slideDown {
  from { opacity: 0; transform: translateY(-5px); }
  to { opacity: 1; transform: translateY(0); }
}

.model-grid-compact {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

.model-card-compact {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 8px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s ease;
}

.model-card-compact:hover {
  border-color: rgba(255, 255, 255, 0.1);
}

.model-card-compact.active {
  border-color: #ef4444;
  background: rgba(239, 68, 68, 0.05);
}

.model-header-compact {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.model-name-compact {
  font-size: 0.75rem;
  font-weight: 600;
}

.model-size-compact {
  font-size: 0.65rem;
  color: var(--text-muted);
  font-family: var(--font-mono);
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
  border-color: rgba(255, 255, 255, 0.1);
}

.quality-card.active {
  border-color: #ef4444;
  background: rgba(239, 68, 68, 0.04);
}

.quality-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-primary);
}

.quality-desc {
  font-size: 0.65rem;
  color: var(--text-muted);
}

.start-btn {
  width: 100%;
  padding: 10px;
  font-size: 0.85rem;
}

.download-btn {
  background: rgba(239, 68, 68, 0.15);
  color: #f87171;
  border: 1px solid rgba(239, 68, 68, 0.25);
  box-shadow: 0 4px 10px rgba(239, 68, 68, 0.05);
}

.download-btn:hover:not(:disabled) {
  background: rgba(239, 68, 68, 0.25);
  border-color: #ef4444;
  color: white;
  transform: translateY(-1px);
}

/* Download progress bar container styles */
.download-progress-container {
  padding: 20px;
  background: rgba(15, 21, 36, 0.6);
  border: 1px solid rgba(239, 68, 68, 0.2);
  box-shadow: 0 8px 30px rgba(0,0,0,0.3);
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
  border: 2px solid rgba(239, 68, 68, 0.2);
  border-top-color: #ef4444;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.dl-status-title-box h4 {
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--text-primary);
}

.dl-percentage {
  font-family: var(--font-mono);
  font-size: 1rem;
  font-weight: 700;
  color: #ef4444;
}

.youtube-fill {
  background: linear-gradient(to right, #ef4444, #f43f5e) !important;
  box-shadow: 0 0 10px rgba(239, 68, 68, 0.6) !important;
}

.dl-progress-hints {
  font-size: 0.7rem;
  color: var(--text-muted);
  line-height: 1.4;
  border-top: 1px dashed var(--border-color);
  padding-top: 8px;
  margin-top: 4px;
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
  background: linear-gradient(135deg, #ef4444 0%, #f43f5e 100%);
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
  background: linear-gradient(to right, #ef4444, #f43f5e);
  box-shadow: 0 0 10px rgba(239, 68, 68, 0.5);
  border-radius: 3px;
  transition: width 0.4s ease;
}

.progress-percentage-label {
  font-family: var(--font-mono);
  font-size: 1.125rem;
  font-weight: 600;
  color: #ef4444;
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
  background: #ef4444;
  border-radius: 1.5px;
  animation: wave 1.2s ease-in-out infinite alternate;
}

@keyframes wave {
  0% {
    height: 8px;
    background: #ef4444;
  }
  100% {
    height: 32px;
    background: #f43f5e;
  }
}
</style>
