<template>
  <div class="glass-panel upload-container">
    <div 
      class="dropzone"
      :class="{ 'drag-over': isDragging, 'has-file': file }"
      @dragover.prevent="onDragOver"
      @dragleave.prevent="onDragLeave"
      @drop.prevent="onDrop"
      @click="triggerFileInput"
    >
      <input 
        type="file" 
        ref="fileInput" 
        class="file-input" 
        accept="audio/*,video/*" 
        @change="onFileSelected"
      />
      
      <div class="upload-icon-container">
        <svg xmlns="http://www.w3.org/2000/svg" class="upload-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
        </svg>
      </div>

      <div v-if="!file" class="dropzone-text">
        <h3>拖拽音频或视频到此处</h3>
        <p>支持 MP3, WAV, M4A, MP4, MKV, AVI 等格式</p>
        <button class="btn btn-secondary select-btn">选择文件</button>
      </div>
      <div v-else class="file-details">
        <div class="file-icon-box">
          <svg v-if="isAudio" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="file-type-icon">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" />
          </svg>
          <svg v-else xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="file-type-icon">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
          </svg>
        </div>
        <div class="file-meta">
          <span class="file-name">{{ file.name }}</span>
          <span class="file-size">{{ formatBytes(file.size) }}</span>
        </div>
        <button class="btn-clear" @click.stop="clearFile" title="移除文件">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="close-icon">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
          </svg>
        </button>
      </div>
    </div>

    <!-- Whisper Options -->
    <div class="options-container" v-if="file">
      <div class="option-row">
        <label class="label-heading">Whisper 识别模型</label>
        <div class="model-grid">
          <div 
            v-for="model in models" 
            :key="model.value" 
            class="model-card"
            :class="{ 'active': selectedModel === model.value }"
            @click="selectedModel = model.value"
          >
            <div class="model-header">
              <span class="model-name">{{ model.name }}</span>
              <span class="model-badge badge" :class="model.badgeClass">{{ model.speed }}</span>
            </div>
            <p class="model-desc">{{ model.desc }}</p>
            <span class="model-size">{{ model.size }}</span>
          </div>
        </div>
      </div>

      <!-- Action Area -->
      <div class="action-footer">
        <button 
          class="btn btn-primary start-btn" 
          :disabled="isTranscribing" 
          @click="startProcessing"
        >
          <span v-if="isTranscribing" class="loader-spinner"></span>
          <span>{{ isTranscribing ? '正在提取并转录中...' : '开始语音转文字' }}</span>
        </button>
      </div>
    </div>

    <!-- Progress Indicator -->
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
    default: '正在上传并进行预处理...'
  },
  progressPercentage: {
    type: Number,
    default: 0
  }
});

const emit = defineEmits(['start-transcription', 'file-selected', 'file-cleared']);

const fileInput = ref(null);
const file = ref(null);
const isDragging = ref(false);
const selectedModel = ref('base');

const models = [
  { value: 'tiny', name: 'Tiny (极速)', size: '大小: 75 MB', speed: '速度极快', badgeClass: 'badge-cyan', desc: '速度最快，精度一般。适用于清晰的普通话。' },
  { value: 'base', name: 'Base (推荐)', size: '大小: 140 MB', speed: '平衡', badgeClass: 'badge-purple', desc: '速度与准确度最平衡的推荐选项，适合大多数场景。' },
  { value: 'small', name: 'Small (高精度)', size: '大小: 460 MB', speed: '较慢', badgeClass: 'badge-success', desc: '精度较高，对噪音、口音有更好的识别力。' }
];

const isAudio = computed(() => {
  if (!file.value) return true;
  return file.value.type.startsWith('audio');
});

const triggerFileInput = () => {
  if (!props.isTranscribing) {
    fileInput.value.click();
  }
};

const onFileSelected = (event) => {
  const selectedFile = event.target.files[0];
  if (selectedFile) {
    setFile(selectedFile);
  }
};

const onDragOver = () => {
  if (!props.isTranscribing) {
    isDragging.value = true;
  }
};

const onDragLeave = () => {
  isDragging.value = false;
};

const onDrop = (event) => {
  if (props.isTranscribing) return;
  isDragging.value = false;
  const droppedFile = event.dataTransfer.files[0];
  if (droppedFile) {
    setFile(droppedFile);
  }
};

const setFile = (selectedFile) => {
  file.value = selectedFile;
  emit('file-selected', selectedFile);
};

const clearFile = () => {
  file.value = null;
  if (fileInput.value) fileInput.value.value = '';
  emit('file-cleared');
};

const startProcessing = () => {
  if (!file.value || props.isTranscribing) return;
  emit('start-transcription', {
    file: file.value,
    modelType: selectedModel.value
  });
};

const formatBytes = (bytes, decimals = 2) => {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
};
</script>

<style scoped>
.upload-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
  position: relative;
}

.dropzone {
  border: 2px dashed var(--border-color);
  border-radius: var(--border-radius-lg);
  padding: 40px 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  text-align: center;
  background: rgba(8, 11, 17, 0.3);
}

.dropzone:hover {
  border-color: var(--accent-cyan);
  background: rgba(6, 182, 212, 0.03);
}

.dropzone.drag-over {
  border-color: var(--accent-purple);
  background: rgba(139, 92, 246, 0.06);
  transform: scale(0.99);
}

.dropzone.has-file {
  border-style: solid;
  border-color: rgba(6, 182, 212, 0.2);
  background: rgba(6, 182, 212, 0.02);
  padding: 24px;
}

.file-input {
  display: none;
}

.upload-icon-container {
  width: 64px;
  height: 64px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 16px;
  transition: all 0.3s ease;
}

.dropzone:hover .upload-icon-container {
  background: rgba(6, 182, 212, 0.1);
  border-color: rgba(6, 182, 212, 0.3);
  transform: translateY(-2px);
}

.upload-icon {
  width: 28px;
  height: 28px;
  color: var(--text-secondary);
  transition: color 0.3s ease;
}

.dropzone:hover .upload-icon {
  color: var(--accent-cyan);
}

.dropzone-text h3 {
  font-size: 1.125rem;
  margin-bottom: 8px;
  color: var(--text-primary);
}

.dropzone-text p {
  font-size: 0.875rem;
  color: var(--text-muted);
  margin-bottom: 20px;
}

.select-btn {
  pointer-events: none; /* Make click pass through to dropzone */
}

/* File Details */
.file-details {
  display: flex;
  align-items: center;
  width: 100%;
  gap: 16px;
  text-align: left;
}

.file-icon-box {
  width: 48px;
  height: 48px;
  border-radius: var(--border-radius-md);
  background: rgba(6, 182, 212, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.file-type-icon {
  width: 24px;
  height: 24px;
  color: var(--accent-cyan);
}

.file-meta {
  display: flex;
  flex-direction: column;
  flex-grow: 1;
  min-width: 0;
}

.file-name {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.file-size {
  font-size: 0.8rem;
  color: var(--text-muted);
  margin-top: 2px;
}

.btn-clear {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--text-muted);
  padding: 8px;
  border-radius: 50%;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-clear:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--accent-error);
}

.close-icon {
  width: 20px;
  height: 20px;
}

/* Options */
.options-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
  animation: fadeInUp 0.4s ease forwards;
}

.option-row {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.label-heading {
  font-family: var(--font-heading);
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--text-secondary);
  letter-spacing: 0.05em;
  text-transform: uppercase;
}

.model-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px;
}

.model-card {
  background: rgba(255, 255, 255, 0.02);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 16px;
  cursor: pointer;
  display: flex;
  flex-direction: column;
  transition: all 0.25s ease;
}

.model-card:hover {
  background: rgba(255, 255, 255, 0.04);
  border-color: rgba(255, 255, 255, 0.1);
}

.model-card.active {
  background: rgba(139, 92, 246, 0.06);
  border-color: rgba(139, 92, 246, 0.4);
  box-shadow: 0 0 15px rgba(139, 92, 246, 0.1);
}

.model-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.model-name {
  font-weight: 600;
  font-size: 0.95rem;
}

.model-badge {
  font-size: 0.65rem;
  padding: 2px 6px;
}

.model-desc {
  font-size: 0.75rem;
  color: var(--text-secondary);
  line-height: 1.4;
  margin-bottom: 8px;
  flex-grow: 1;
}

.model-size {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-family: var(--font-mono);
}

.action-footer {
  display: flex;
  justify-content: flex-end;
  margin-top: 8px;
}

.start-btn {
  width: 100%;
  padding: 14px 28px;
}

.loader-spinner {
  width: 18px;
  height: 18px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
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
  background: var(--accent-gradient);
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
  background: var(--accent-gradient);
  box-shadow: 0 0 10px rgba(6, 182, 212, 0.5);
  border-radius: 3px;
  transition: width 0.4s ease;
}

.progress-percentage-label {
  font-family: var(--font-mono);
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--accent-cyan);
  margin-bottom: 16px;
}

/* Wave Visualizer */
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
  background: var(--accent-cyan);
  border-radius: 1.5px;
  animation: wave 1.2s ease-in-out infinite alternate;
}

@keyframes wave {
  0% {
    height: 8px;
    background: var(--accent-cyan);
  }
  100% {
    height: 32px;
    background: var(--accent-purple);
  }
}
</style>
