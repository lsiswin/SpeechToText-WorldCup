<template>
  <div class="glass-panel video-burner-container">
    <div class="burner-grid">
      <!-- Left Column: Source selection and Styling settings -->
      <div class="settings-panel">
        <h3 class="panel-section-title">1. 选择视频源</h3>

        <!-- Tabs: Local Video / Online Link -->
        <div class="source-tab-selector">
          <button 
            class="source-tab-btn" 
            :class="{ active: sourceMode === 'local' }"
            @click="sourceMode = 'local'"
            :disabled="isProcessing"
          >
            本地视频文件
          </button>
          <button 
            class="source-tab-btn" 
            :class="{ active: sourceMode === 'online' }"
            @click="sourceMode = 'online'"
            :disabled="isProcessing"
          >
            在线视频链接
          </button>
        </div>

        <!-- Mode A: Local Video Dropzone -->
        <div v-if="sourceMode === 'local'" class="local-dropzone-area">
          <div 
            v-if="!localFile"
            class="dropzone"
            :class="{ dragover: isDragOver }"
            @dragover.prevent="isDragOver = true"
            @dragleave.prevent="isDragOver = false"
            @drop.prevent="onFileDrop"
            @click="triggerFileInput"
          >
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="upload-icon">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 4v16M17 4v16M3 8h18M3 16h18" />
            </svg>
            <p>拖拽 MP4 格式视频文件到此处，或<span>点击浏览文件</span></p>
            <span class="file-limits-hint">支持 .mp4 格式视频，大小不超过 200MB</span>
            <input 
              type="file" 
              ref="fileInput" 
              class="hidden-file-input" 
              accept="video/mp4" 
              @change="onFileSelected"
            />
          </div>
          
          <!-- Selected Local File Card -->
          <div v-else class="selected-file-card glass-panel">
            <div class="file-details">
              <span class="file-icon">🎬</span>
              <div class="file-info-text">
                <span class="file-name" :title="localFile.name">{{ localFile.name }}</span>
                <span class="file-size">{{ formatBytes(localFile.size) }}</span>
              </div>
            </div>
            <button class="clear-file-btn" @click="clearLocalFile" :disabled="isProcessing">×</button>
          </div>
        </div>

        <!-- Mode B: Online URL Input -->
        <div v-else class="online-input-area">
          <div class="url-input-wrapper">
            <input 
              type="text" 
              v-model="onlineUrl" 
              placeholder="请输入 YouTube 或 B站/b23.tv 视频链接..."
              class="url-input-box"
              :disabled="isProcessing"
            />
            <button 
              v-if="onlineUrl && !isProcessing" 
              class="clear-input-btn" 
              @click="onlineUrl = ''"
            >×</button>
          </div>
          <span class="input-hint-text">自动支持 B站/YouTube 英文短片自动下载压制</span>
        </div>

        <!-- 字幕样式设定 (Divider) -->
        <h3 class="panel-section-title spacing-top">2. 字幕样式与翻译设定</h3>
        <div class="styling-card glass-panel">
          <!-- Translation Switch -->
          <div class="setting-row">
            <label class="setting-label">翻译选项</label>
            <div class="translation-toggle-group">
              <button 
                class="toggle-btn"
                :class="{ active: translateToChinese }"
                @click="translateToChinese = true"
                :disabled="isProcessing"
              >
                英译中 (翻译并压制中文)
              </button>
              <button 
                class="toggle-btn"
                :class="{ active: !translateToChinese }"
                @click="translateToChinese = false"
                :disabled="isProcessing"
              >
                保留原语 (仅识别声轨)
              </button>
            </div>
          </div>

          <!-- FontSize Slider -->
          <div class="setting-row">
            <div class="slider-label-row">
              <label class="setting-label">字体大小</label>
              <span class="slider-value-badge">{{ fontSize }} px</span>
            </div>
            <input 
              type="range" 
              v-model.number="fontSize" 
              min="16" 
              max="32" 
              step="1"
              class="styling-range-slider"
              :disabled="isProcessing"
            />
          </div>

          <!-- Color Circle Selector -->
          <div class="setting-row">
            <label class="setting-label">字体颜色</label>
            <div class="color-palette-selector">
              <button 
                v-for="color in fontColors" 
                :key="color.value"
                class="color-dot"
                :style="{ backgroundColor: color.value }"
                :class="{ active: selectedColor === color.hex }"
                @click="selectedColor = color.hex"
                :title="color.name"
                :disabled="isProcessing"
              >
                <span class="check-mark" v-if="selectedColor === color.hex">✓</span>
              </button>
            </div>
          </div>

          <!-- Subtitle Style (Outline / Box) -->
          <div class="setting-row">
            <label class="setting-label">背景版式</label>
            <div class="style-toggle-group">
              <div 
                class="style-choice-box"
                :class="{ active: subtitleStyle === 'outline' }"
                @click="!isProcessing && (subtitleStyle = 'outline')"
              >
                <div class="choice-visual outline-preview">字幕 Outline</div>
                <span>双向描边</span>
              </div>
              <div 
                class="style-choice-box"
                :class="{ active: subtitleStyle === 'box' }"
                @click="!isProcessing && (subtitleStyle = 'box')"
              >
                <div class="choice-visual box-preview">字幕 Box</div>
                <span>黑框背板</span>
              </div>
            </div>
          </div>

          <!-- Position Alignment -->
          <div class="setting-row">
            <label class="setting-label">字幕纵向位置</label>
            <div class="position-toggle-group">
              <button 
                class="toggle-btn"
                :class="{ active: position === 'bottom' }"
                @click="position = 'bottom'"
                :disabled="isProcessing"
              >
                屏幕底部
              </button>
              <button 
                class="toggle-btn"
                :class="{ active: position === 'top' }"
                @click="position = 'top'"
                :disabled="isProcessing"
              >
                屏幕顶部 (防遮挡)
              </button>
            </div>
          </div>
        </div>

        <button 
          class="btn btn-primary start-process-btn"
          :disabled="isProcessing || !hasValidSource"
          @click="submitBurnTask"
        >
          <span class="btn-glow-orb"></span>
          🎬 开始自动智能压制硬字幕
        </button>
      </div>

      <!-- Right Column: Live terminal status console -->
      <div class="terminal-panel">
        <h3 class="panel-section-title">控制台实时压制日志</h3>
        
        <!-- Monospace Console log viewer -->
        <div class="terminal-shell" ref="terminalRef">
          <div class="terminal-header">
            <span class="dot red"></span>
            <span class="dot yellow"></span>
            <span class="dot green"></span>
            <span class="shell-title">ffmpeg_encoder_session.sh</span>
          </div>
          <div class="terminal-body">
            <!-- Idle State -->
            <div v-if="logs.length === 0" class="console-idle-prompt">
              <span class="cyan-prompt">$</span> waiting_for_job_submission...<br/>
              <span class="gray-text">提示：在左侧上传视频并配置样式后，点击“开始压制”。<br/>系统将在此处输出包括：音轨提取、离线Whisper识别、AI大模型翻译、ASS文件编译和FFmpeg重编码渲染的底层进程日志。</span>
            </div>

            <!-- Log Streams -->
            <div 
              v-else 
              v-for="(log, idx) in logs" 
              :key="idx" 
              class="console-log-line animate-text"
              :class="log.type"
            >
              <span class="log-time">[{{ log.time }}]</span>
              <span class="log-text">{{ log.text }}</span>
            </div>
          </div>
        </div>

        <!-- Terminal Progress Bar Track -->
        <div class="progress-section" v-if="isProcessing">
          <div class="progress-header-row">
            <div class="progress-status-title">
              <span class="shell-spinner"></span>
              <span>{{ currentStatusText }}</span>
            </div>
            <span class="progress-percent-number">{{ Math.round(overallProgress) }}%</span>
          </div>
          <div class="progress-track">
            <div class="progress-bar-fill orange-glow" :style="{ width: overallProgress + '%' }"></div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue';

const sourceMode = ref('local'); // 'local' | 'online'
const localFile = ref(null);
const onlineUrl = ref('');
const isDragOver = ref(false);
const fileInput = ref(null);

// Styling properties
const translateToChinese = ref(true);
const fontSize = ref(20);
const selectedColor = ref('ffff00'); // Default Yellow
const subtitleStyle = ref('box'); // 'outline' | 'box'
const position = ref('bottom'); // 'bottom' | 'top'

// Process & Console status
const isProcessing = ref(false);
const overallProgress = ref(0);
const currentStatusText = ref('排队中...');
const logs = ref([]);
const terminalRef = ref(null);
let pollingTimer = null;

const fontColors = [
  { name: '白色', value: '#ffffff', hex: 'ffffff' },
  { name: '明黄', value: '#ffd600', hex: 'ffff00' },
  { name: '天蓝', value: '#00e5ff', hex: '00ffff' },
  { name: '淡绿', value: '#00e676', hex: '00ff00' }
];

const hasValidSource = computed(() => {
  if (sourceMode.value === 'local') {
    return localFile.value !== null;
  } else {
    const url = onlineUrl.value.trim();
    return url.includes('youtube.com/') || url.includes('youtu.be/') || url.includes('bilibili.com/') || url.includes('b23.tv/');
  }
});

const themeColor = computed(() => {
  if (sourceMode.value === 'online') {
    const url = onlineUrl.value.toLowerCase();
    if (url.includes('bilibili.com') || url.includes('b23.tv')) {
      return '#fb7299';
    }
  }
  return '#ef4444'; // Orange-red default
});

// File Handlers
const triggerFileInput = () => {
  if (fileInput.value) {
    fileInput.value.click();
  }
};

const onFileSelected = (e) => {
  const files = e.target.files;
  if (files && files.length > 0) {
    localFile.value = files[0];
  }
};

const onFileDrop = (e) => {
  isDragOver.value = false;
  const files = e.dataTransfer.files;
  if (files && files.length > 0) {
    const file = files[0];
    if (file.type === 'video/mp4' || file.name.endsWith('.mp4')) {
      localFile.value = file;
    } else {
      alert('系统目前仅支持压制标准 .mp4 格式的视频。');
    }
  }
};

const clearLocalFile = () => {
  localFile.value = null;
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};

// Log logger
const writeLog = (text, type = 'info') => {
  const time = new Date().toLocaleTimeString();
  logs.value.push({ time, text, type });
  nextTick(() => {
    if (terminalRef.value) {
      terminalRef.value.scrollTop = terminalRef.value.scrollHeight;
    }
  });
};

// Submit Task pipeline
const submitBurnTask = async () => {
  isProcessing.value = true;
  overallProgress.value = 0;
  logs.value = [];
  
  try {
    let videoSourceUrl = '';

    // Step 1: Handle upload if local file selected
    if (sourceMode.value === 'local') {
      writeLog('检查到本地视频文件。正在上传视频到本地服务器进行预处理...', 'info');
      const formData = new FormData();
      formData.append('file', localFile.value);
      formData.append('modelType', 'base'); // Warmup model upload format

      const xhr = new XMLHttpRequest();
      xhr.open('POST', '/api/transcription/transcribe'); // Use upload pipeline to save local file to temp directory
      
      const uploadPromise = new Promise((resolve, reject) => {
        xhr.upload.addEventListener('progress', (e) => {
          if (e.lengthComputable) {
            const percent = Math.round((e.loaded / e.total) * 100);
            overallProgress.value = percent * 0.15; // Upload maps to first 15%
            currentStatusText.value = `文件上传中 (${percent}%)...`;
          }
        });
        
        xhr.onload = () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const data = JSON.parse(xhr.responseText);
              resolve(data.fileName || data.FileName);
            } catch (err) {
              reject(new Error('上传响应解析错误'));
            }
          } else {
            reject(new Error(xhr.responseText || `HTTP 错误 ${xhr.status}`));
          }
        };
        xhr.onerror = () => reject(new Error('文件传输连接中断，请检查后端运行状态。'));
      });

      xhr.send(formData);
      videoSourceUrl = await uploadPromise;
      writeLog(`本地视频文件上传成功！临时物理文件名: ${videoSourceUrl}`, 'success');
    } else {
      videoSourceUrl = onlineUrl.value.trim();
      writeLog(`检查到在线视频链接。已将源地址提交到下载分配队列: ${videoSourceUrl}`, 'info');
    }

    // Step 2: Trigger backend burn-subtitles task
    writeLog('正在配置字幕渲染滤镜与样式参数，发起后台合成任务...', 'info');
    const startResponse = await fetch('/api/transcription/video/burn-subtitles', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        videoUrl: videoSourceUrl,
        modelType: 'base',
        translateToChinese: translateToChinese.value,
        fontSize: fontSize.value,
        fontColor: selectedColor.value,
        subtitleStyle: subtitleStyle.value,
        position: position.value
      })
    });

    if (!startResponse.ok) {
      const errText = await startResponse.ok;
      throw new Error(errText || '无法启动字幕压制后台任务。');
    }

    const { taskId } = await startResponse.json();
    writeLog(`已成功创建字幕压制后台工作线程，工作任务ID: ${taskId}`, 'success');

    // Step 3: Poll status
    let lastStatusText = '';
    pollingTimer = setInterval(async () => {
      try {
        const statusResponse = await fetch(`/api/transcription/youtube/download/status?taskId=${encodeURIComponent(taskId)}`);
        if (!statusResponse.ok) return;

        const data = await statusResponse.json();
        
        overallProgress.value = data.progress;
        currentStatusText.value = data.statusText;

        if (data.statusText && data.statusText !== lastStatusText) {
          writeLog(data.statusText, 'process');
          lastStatusText = data.statusText;
        }

        if (data.isComplete) {
          clearInterval(pollingTimer);
          writeLog('视频硬字幕压制重新编码完成！正在唤起浏览器保存文件...', 'success');
          
          setTimeout(() => {
            const finalUrl = `/api/transcription/youtube/download/file?taskId=${encodeURIComponent(taskId)}`;
            const a = document.createElement('a');
            a.href = finalUrl;
            a.setAttribute('download', '');
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);

            isProcessing.value = false;
          }, 1000);
        } else if (data.error) {
          clearInterval(pollingTimer);
          writeLog(`压制合成中断，错误信息: ${data.error}`, 'error');
          alert('字幕压制失败: ' + data.error);
          isProcessing.value = false;
        }
      } catch (err) {
        console.error('Polling status error:', err);
      }
    }, 1000);

  } catch (error) {
    writeLog(`任务调度发生异常: ${error.message}`, 'error');
    alert('压制任务提交失败: ' + error.message);
    isProcessing.value = false;
  }
};

const formatBytes = (bytes) => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};
</script>

<style scoped>
.video-burner-container {
  min-height: 480px;
}

.burner-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 32px;
}

@media (min-width: 1024px) {
  .burner-grid {
    grid-template-columns: 460px 1fr;
  }
}

.panel-section-title {
  font-size: 1.05rem;
  color: var(--text-secondary);
  font-weight: 600;
  margin-bottom: 12px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.spacing-top {
  margin-top: 24px;
}

/* Source Selection Tabs */
.source-tab-selector {
  display: flex;
  background: rgba(8, 11, 17, 0.4);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-sm);
  padding: 4px;
  margin-bottom: 16px;
}

.source-tab-btn {
  flex: 1;
  background: transparent;
  border: none;
  padding: 8px;
  font-size: 0.85rem;
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.source-tab-btn.active {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

/* Local dropzone and file card */
.local-dropzone-area {
  margin-bottom: 16px;
}

.dropzone {
  border: 2px dashed var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 24px 16px;
  text-align: center;
  cursor: pointer;
  background: rgba(15, 21, 36, 0.2);
  transition: all 0.25s ease;
}

.dropzone:hover, .dropzone.dragover {
  border-color: var(--accent-purple);
  background: rgba(139, 92, 246, 0.03);
}

.upload-icon {
  width: 40px;
  height: 40px;
  color: var(--text-muted);
  margin-bottom: 12px;
}

.dropzone p {
  font-size: 0.875rem;
  color: var(--text-secondary);
  margin-bottom: 6px;
}

.dropzone p span {
  color: var(--accent-purple);
  font-weight: 600;
}

.file-limits-hint {
  font-size: 0.725rem;
  color: var(--text-muted);
}

.hidden-file-input {
  display: none;
}

.selected-file-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: rgba(139, 92, 246, 0.02) !important;
  border-color: rgba(139, 92, 246, 0.2);
}

.file-details {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
}

.file-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.file-info-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.file-name {
  font-size: 0.85rem;
  color: var(--text-primary);
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.file-size {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.clear-file-btn {
  background: transparent;
  border: none;
  font-size: 1.5rem;
  color: var(--text-muted);
  cursor: pointer;
  padding: 0 4px;
  line-height: 1;
}

.clear-file-btn:hover {
  color: var(--text-primary);
}

/* Online link box */
.online-input-area {
  margin-bottom: 16px;
}

.url-input-wrapper {
  display: flex;
  align-items: center;
  background: rgba(8, 11, 17, 0.5);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 8px 14px;
}

.url-input-box {
  background: transparent;
  border: none;
  outline: none;
  color: var(--text-primary);
  font-size: 0.9rem;
  width: 100%;
}

.clear-input-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 1.15rem;
}

.input-hint-text {
  font-size: 0.725rem;
  color: var(--text-muted);
  margin-top: 6px;
  display: block;
  padding-left: 4px;
}

/* Custom Subtitle styling panel */
.styling-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: rgba(15, 21, 36, 0.4) !important;
  padding: 20px;
}

.setting-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.setting-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
}

.translation-toggle-group, .position-toggle-group {
  display: flex;
  background: rgba(8, 11, 17, 0.4);
  padding: 3px;
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.toggle-btn {
  flex: 1;
  background: transparent;
  border: none;
  padding: 8px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
}

.toggle-btn.active {
  background: var(--accent-purple);
  color: white;
  box-shadow: 0 2px 6px rgba(139, 92, 246, 0.3);
}

/* Range Slider */
.slider-label-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.slider-value-badge {
  font-family: var(--font-mono);
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--accent-purple);
}

.styling-range-slider {
  width: 100%;
  accent-color: var(--accent-purple);
}

/* Color palettes */
.color-palette-selector {
  display: flex;
  gap: 12px;
}

.color-dot {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  cursor: pointer;
  border: 2px solid transparent;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  padding: 0;
}

.color-dot.active {
  border-color: white;
  transform: scale(1.15);
  box-shadow: 0 0 8px rgba(255, 255, 255, 0.4);
}

.check-mark {
  font-size: 0.75rem;
  color: #000;
  font-weight: bold;
}

/* Style choices Outline / Box */
.style-toggle-group {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 12px;
}

.style-choice-box {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-sm);
  padding: 10px;
  cursor: pointer;
  text-align: center;
  transition: all 0.2s ease;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.style-choice-box:hover {
  border-color: rgba(255, 255, 255, 0.1);
}

.style-choice-box.active {
  border-color: var(--accent-purple);
  background: rgba(139, 92, 246, 0.04);
}

.choice-visual {
  height: 38px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  font-weight: bold;
  background: rgba(0, 0, 0, 0.6);
  font-family: 'Microsoft YaHei', sans-serif;
  color: #ffff00;
}

.outline-preview {
  text-shadow: 1px 1px 0 #000, -1px 1px 0 #000, 1px -1px 0 #000, -1px -1px 0 #000;
}

.box-preview {
  background: rgba(0, 0, 0, 0.8) !important;
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.style-choice-box span {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.style-choice-box.active span {
  color: var(--text-primary);
  font-weight: 600;
}

/* Start button */
.start-process-btn {
  width: 100%;
  padding: 14px;
  font-size: 0.9rem;
  margin-top: 20px;
  background: linear-gradient(135deg, var(--accent-purple) 0%, #1e1b4b 100%);
  position: relative;
  overflow: hidden;
}

.btn-glow-orb {
  position: absolute;
  top: -20px;
  left: -20px;
  width: 60px;
  height: 60px;
  background: radial-gradient(circle, rgba(139,92,246,0.6) 0%, rgba(0,0,0,0) 70%);
  filter: blur(10px);
  pointer-events: none;
}

/* Right Column: Console Log Viewer */
.terminal-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 480px;
}

.terminal-shell {
  flex-grow: 1;
  background: #020408;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  box-shadow: inset 0 4px 20px rgba(0, 0, 0, 0.6);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  height: 380px;
}

.terminal-header {
  background: #0d1117;
  padding: 8px 16px;
  border-bottom: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  gap: 6px;
}

.dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.dot.red { background: #ff5f56; }
.dot.yellow { background: #ffbd2e; }
.dot.green { background: #27c93f; }

.shell-title {
  font-family: var(--font-mono);
  font-size: 0.725rem;
  color: var(--text-muted);
  margin-left: 12px;
}

.terminal-body {
  padding: 16px;
  font-family: var(--font-mono);
  font-size: 0.8rem;
  line-height: 1.6;
  overflow-y: auto;
  flex-grow: 1;
  color: #abb2bf;
}

.console-idle-prompt {
  line-height: 1.8;
}

.cyan-prompt {
  color: #56b6c2;
  font-weight: bold;
}

.gray-text {
  color: var(--text-muted);
}

.console-log-line {
  margin-bottom: 4px;
  word-break: break-all;
  white-space: pre-wrap;
}

.console-log-line.info {
  color: #abb2bf;
}

.console-log-line.success {
  color: #98c379;
}

.console-log-line.process {
  color: #61afef;
}

.console-log-line.error {
  color: #e06c75;
  background: rgba(224, 108, 117, 0.05);
  border-left: 2px solid #e06c75;
  padding-left: 6px;
}

.log-time {
  color: #5c6370;
  margin-right: 8px;
  flex-shrink: 0;
}

.animate-text {
  animation: logLineIn 0.15s ease-out forwards;
}

@keyframes logLineIn {
  from { opacity: 0; transform: translateX(-4px); }
  to { opacity: 1; transform: translateX(0); }
}

/* Progress bar area */
.progress-section {
  margin-top: 16px;
  background: rgba(15, 21, 36, 0.6);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 16px;
}

.progress-header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.progress-status-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.8rem;
  color: var(--text-secondary);
  font-weight: 600;
}

.shell-spinner {
  width: 14px;
  height: 14px;
  border: 2px solid rgba(139, 92, 246, 0.15);
  border-top-color: var(--accent-purple);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.progress-percent-number {
  font-family: var(--font-mono);
  font-size: 0.95rem;
  font-weight: 700;
  color: var(--accent-purple);
}

.progress-track {
  width: 100%;
  height: 6px;
  background: var(--bg-primary);
  border-radius: 3px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  border-radius: 3px;
  transition: width 0.4s ease;
}

.orange-glow {
  background: linear-gradient(to right, var(--accent-purple), #a855f7);
  box-shadow: 0 0 8px var(--accent-purple);
}
</style>
