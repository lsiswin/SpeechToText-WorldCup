<template>
  <div class="app-container">
    <!-- Navigation Header (Sticky, only show when inside a tool view) -->
    <header class="nav-header" v-if="currentView !== 'home'">
      <div class="breadcrumbs">
        <span class="breadcrumb-home" @click="currentView = 'home'">
          🛠️ 智能工具箱
        </span>
        <span class="breadcrumb-separator">/</span>
        <span class="breadcrumb-current">{{ currentToolName }}</span>
      </div>
      <button class="btn btn-secondary btn-back-home" @click="currentView = 'home'">
        返回工具首页
      </button>
    </header>

    <!-- App Header (Show only on homepage) -->
    <header class="app-header" v-else>
      <div class="logo-box">
        <div class="logo-orb">🛠️</div>
        <div class="logo-text">
          <h1>智能多功能工具箱</h1>
          <p>基于本地 AI 与先进媒体处理的实用效率工具平台</p>
        </div>
      </div>
      <div class="system-status">
        <span class="badge badge-purple">本地运行</span>
        <span class="badge badge-cyan">v1.1.0</span>
      </div>
    </header>

    <!-- Main Content Switcher with Transitions -->
    <transition name="fade-slide" mode="out-in">
      <!-- HOMEPAGE VIEW -->
      <div v-if="currentView === 'home'" key="home" class="homepage-view animate-fade-in">
        <!-- Search & Filter section -->
        <div class="search-filter-section">
          <div class="search-bar-wrapper">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="search-icon">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input 
              type="text" 
              v-model="searchQuery" 
              placeholder="搜索您需要的工具，例如：转文字、下载、格式转换..."
              class="search-input"
            />
          </div>
          <div class="filter-row">
            <button 
              v-for="cat in categories" 
              :key="cat.value"
              class="filter-btn"
              :class="{ active: activeCategory === cat.value }"
              @click="activeCategory = cat.value"
            >
              {{ cat.name }}
            </button>
          </div>
        </div>

        <!-- Tools Cards Grid -->
        <div v-if="filteredTools.length > 0" class="tools-grid">
          <div 
            v-for="tool in filteredTools" 
            :key="tool.id"
            class="tool-card glass-panel"
            :style="{ '--hover-color': tool.color, '--glow-color': tool.glow }"
            @click="openTool(tool)"
          >
            <div class="tool-card-header">
              <div class="tool-icon-wrapper">
                <span v-html="tool.icon"></span>
              </div>
              <span class="badge" :class="tool.badgeClass">{{ tool.badgeText }}</span>
            </div>
            <div class="tool-card-content">
              <h3>{{ tool.title }}</h3>
              <p>{{ tool.description }}</p>
            </div>
            <div class="tool-card-footer">
              <div class="tool-tags">
                <span v-for="tag in tool.tags" :key="tag" class="tool-tag">{{ tag }}</span>
              </div>
              <span class="btn-text-action" v-if="tool.status === 'active'">
                立即使用 &rarr;
              </span>
              <span style="color: var(--text-muted); font-size: 0.8rem;" v-else>
                敬请期待
              </span>
            </div>
          </div>
        </div>

        <!-- No search results state -->
        <div v-else class="no-results-box glass-panel animate-fade-in">
          <svg class="no-results-icon" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.172 16.172a4 4 0 015.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          <h3>没有找到相关工具</h3>
          <p>请尝试输入其他关键词，或在上方切换分类过滤器。</p>
        </div>
      </div>

      <!-- LOCAL SPEECH TO TEXT VIEW -->
      <div v-else-if="currentView === 'local-stt'" key="local-stt" class="main-layout">
        <!-- Left: Upload or Player -->
        <div class="col-left">
          <!-- Input upload area -->
          <UploadArea 
            v-if="!localStt.segments.length"
            :is-transcribing="localStt.isTranscribing"
            :progress-status="localStt.progressStatus"
            :progress-details="localStt.progressDetails"
            :progress-percentage="localStt.progressPercentage"
            @file-selected="onLocalFileSelected"
            @file-cleared="onLocalFileCleared"
            @start-transcription="onStartLocalTranscription"
          />

          <!-- Work Panel: Player -->
          <div v-else class="player-panel glass-panel">
            <div class="panel-header">
              <h3>本地媒体播放器</h3>
              <button class="btn-text-action" @click="onLocalFileCleared">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-icon">
                  <path fill-rule="evenodd" d="M9.707 16.707a1 1 0 01-1.414 0l-6-6a1 1 0 010-1.414l6-6a1 1 0 011.414 1.414L5.414 9H17a1 1 0 110 2H5.414l4.293 4.293a1 1 0 010 1.414z" clip-rule="evenodd" />
                </svg>
                <span>重新上传</span>
              </button>
            </div>
            <MediaPlayer 
              ref="localMediaPlayer" 
              :file="localStt.file" 
              :src-url="''"
              @time-update="onLocalTimeUpdate" 
            />
            <div class="media-file-info">
              <span class="label">当前文件:</span>
              <span class="value">{{ localStt.file?.name }}</span>
            </div>
          </div>
        </div>

        <!-- Right: Transcript editor -->
        <div class="col-right">
          <div v-if="!localStt.segments.length" class="empty-transcript-state glass-panel">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="empty-icon">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <h3>暂无识别结果</h3>
            <p>请在左侧上传本地音视频文件，系统将自动使用 Whisper 本地引擎完成语音转录。</p>
          </div>

          <TranscriptViewer 
            v-else
            :segments="localStt.segments"
            :current-time="localStt.currentTime"
            :file-name="localStt.file?.name || '语音识别文档'"
            @seek-to="onLocalSeekTo"
          />
        </div>
      </div>

      <!-- ONLINE VIDEO VIEW -->
      <div v-else-if="currentView === 'online-video'" key="online-video" class="main-layout">
        <!-- Left: URL Input & Card or Player -->
        <div class="col-left">
          <OnlineVideoInput
            v-if="!onlineStt.segments.length"
            :is-transcribing="onlineStt.isTranscribing"
            :progress-status="onlineStt.progressStatus"
            :progress-details="onlineStt.progressDetails"
            :progress-percentage="onlineStt.progressPercentage"
            @start-transcription="onStartOnlineTranscription"
          />

          <!-- Work Panel: Audio stream player -->
          <div v-else class="player-panel glass-panel">
            <div class="panel-header">
              <h3>提取音频播放器</h3>
              <button class="btn-text-action" @click="onOnlineFileCleared">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-icon">
                  <path fill-rule="evenodd" d="M9.707 16.707a1 1 0 01-1.414 0l-6-6a1 1 0 010-1.414l6-6a1 1 0 011.414 1.414L5.414 9H17a1 1 0 110 2H5.414l4.293 4.293a1 1 0 010 1.414z" clip-rule="evenodd" />
                </svg>
                <span>重新解析</span>
              </button>
            </div>
            <MediaPlayer 
              ref="onlineMediaPlayer" 
              :file="null" 
              :src-url="onlineStt.serverAudioUrl"
              @time-update="onOnlineTimeUpdate" 
            />
            
            <!-- Online Video Info Card -->
            <div class="youtube-author-card" :style="{ borderColor: onlineStt.youtubeMetadata.platform === 'bilibili' ? 'rgba(251, 114, 153, 0.15)' : 'rgba(239, 68, 68, 0.15)', background: onlineStt.youtubeMetadata.platform === 'bilibili' ? 'rgba(251, 114, 153, 0.04)' : 'rgba(239, 68, 68, 0.04)' }">
              <div class="avatar-glow-ring" v-if="onlineStt.youtubeMetadata.avatarUrl" :style="{ background: onlineStt.youtubeMetadata.platform === 'bilibili' ? 'linear-gradient(135deg, #fb7299 0%, #ff8e9b 100%)' : 'linear-gradient(135deg, #ef4444 0%, #f43f5e 100%)' }">
                <img :src="onlineStt.youtubeMetadata.avatarUrl" class="author-avatar" alt="Creator Avatar" referrerpolicy="no-referrer" />
              </div>
              <div class="video-info-block">
                <span class="video-title-label" :title="onlineStt.youtubeMetadata.title">{{ onlineStt.youtubeMetadata.title }}</span>
                <span class="badge subtitle-badge" :style="{ color: onlineStt.youtubeMetadata.platform === 'bilibili' ? '#fb7299' : '#f87171', borderColor: onlineStt.youtubeMetadata.platform === 'bilibili' ? 'rgba(251, 114, 153, 0.2)' : 'rgba(239, 68, 68, 0.2)', background: onlineStt.youtubeMetadata.platform === 'bilibili' ? 'rgba(251, 114, 153, 0.1)' : 'rgba(239, 68, 68, 0.1)' }">
                  {{ onlineStt.youtubeMetadata.platform === 'bilibili' ? 'B站 视频音轨' : 'YouTube 视频音轨' }}
                </span>
              </div>
            </div>
          </div>
        </div>

        <!-- Right: Online transcript editor -->
        <div class="col-right">
          <div v-if="!onlineStt.segments.length" class="empty-transcript-state glass-panel">
            <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="empty-icon">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            <h3>暂无识别结果</h3>
            <p>请在左侧输入在线视频链接，您可以直接下载音视频合并文件，或点击语音转录文字以进行云提取和转写。</p>
          </div>

          <TranscriptViewer 
            v-else
            :segments="onlineStt.segments"
            :current-time="onlineStt.currentTime"
            :file-name="onlineStt.youtubeMetadata.title || '在线转录文档'"
            @seek-to="onOnlineSeekTo"
          />
        </div>
      </div>

      <!-- VIDEO SUBTITLE BURNER VIEW -->
      <div v-else-if="currentView === 'video-burner'" key="video-burner">
        <VideoBurner />
      </div>

      <!-- WORLD CUP PREDICTOR VIEW -->
      <div v-else-if="currentView === 'world-cup-predictor'" key="world-cup-predictor">
        <WorldCupPredictor />
      </div>
    </transition>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import UploadArea from './components/UploadArea.vue';
import OnlineVideoInput from './components/OnlineVideoInput.vue';
import MediaPlayer from './components/MediaPlayer.vue';
import TranscriptViewer from './components/TranscriptViewer.vue';
import VideoBurner from './components/VideoBurner.vue';
import WorldCupPredictor from './components/WorldCupPredictor.vue';

// State-based Routing
const currentView = ref('home'); // 'home' | 'local-stt' | 'online-video'
const searchQuery = ref('');
const activeCategory = ref('all');

// Categories filter list
const categories = [
  { name: '全部工具', value: 'all' },
  { name: '语音转换', value: 'audio' },
  { name: '媒体提取', value: 'media' },
  { name: '字幕编辑', value: 'edit' },
  { name: '模拟娱乐', value: 'fun' },
  { name: '开发中占位', value: 'pending' }
];

// Tools registry with metadata and branding colors
const tools = ref([
  {
    id: 'local-stt',
    title: '本地音视频转文字',
    description: '导入本地任意音视频格式文件，调用本地 Whisper 模型进行全离线高精准识别与转写。',
    icon: `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width:24px;height:24px;"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z" /></svg>`,
    color: '#06b6d4',
    glow: 'rgba(6, 182, 212, 0.2)',
    badgeText: '推荐',
    badgeClass: 'badge-cyan',
    category: 'audio',
    tags: ['语音识别', '离线转换', '多格式'],
    status: 'active'
  },
  {
    id: 'online-video',
    title: '在线视频下载与转录',
    description: '支持 YouTube / B站 视频预览与合并，可高速下载 1080P/720P MP4 视频或提取音轨进行离线转写。',
    icon: `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width:24px;height:24px;"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" /></svg>`,
    color: '#fb7299',
    glow: 'rgba(251, 114, 153, 0.2)',
    badgeText: '已升级',
    badgeClass: 'badge-purple',
    category: 'media',
    tags: ['视频下载', '在线提取', 'B站/YT'],
    status: 'active'
  },
  {
    id: 'audio-converter',
    title: '音频格式重采样',
    description: '基于 FFmpeg 的音频格式重采样与格式压缩工具，支持输出 WAV/MP3 格式（计划中）。',
    icon: `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width:24px;height:24px;"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19V6l12-3v13M9 19c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zm12-3c0 1.105-1.343 2-3 2s-3-.895-3-2 1.343-2 3-2 3 .895 3 2zM9 10l12-3" /></svg>`,
    color: '#a855f7',
    glow: 'rgba(168, 85, 247, 0.1)',
    badgeText: '开发中',
    badgeClass: 'badge-success',
    category: 'media',
    tags: ['格式转换', '音频压缩'],
    status: 'pending'
  },
  {
    id: 'video-burner',
    title: '视频智能字幕压制与翻译',
    description: '全自动抓取声轨，将离线 Whisper 识别/DeepSeek 翻译后的字幕压制进视频中，支持自定义排版格式。',
    icon: `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width:24px;height:24px;"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 10l4.553-2.276A1 1 0 0121 8.618v6.764a1 1 0 01-1.447.894L15 14M5 18h8a2 2 0 002-2V8a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" /></svg>`,
    color: '#a855f7',
    glow: 'rgba(168, 85, 247, 0.2)',
    badgeText: '已上线',
    badgeClass: 'badge-purple',
    category: 'edit',
    tags: ['智能字幕', 'DeepSeek翻译', '视频压制'],
    status: 'active'
  },
  {
    id: 'world-cup-predictor',
    title: '美加墨世界杯晋级预测',
    description: '2026年世界杯晋级模拟平台。支持 12 个小组手动对比与积分排行，及 32 强淘汰赛推进生成预测报告。',
    icon: `<svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" style="width:24px;height:24px;"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v13m0-13V6a2 2 0 112 2h-2zm0 0V5a2 2 0 10-2 2h2zm0 0h4m-4 0H8m4 0v13m0 0H8m4 0h4" /></svg>`,
    color: '#fbbf24',
    glow: 'rgba(251, 191, 36, 0.25)',
    badgeText: 'NEW',
    badgeClass: 'badge-purple',
    category: 'fun',
    tags: ['赛事模拟', '小组积分', '淘汰赛对局'],
    status: 'active'
  }
]);

// Realtime search and category filtering logic
const filteredTools = computed(() => {
  return tools.value.filter(tool => {
    // 1. Category check
    if (activeCategory.value !== 'all') {
      if (activeCategory.value === 'pending') {
        if (tool.status !== 'pending') return false;
      } else {
        if (tool.category !== activeCategory.value) return false;
      }
    }
    // 2. Search query check (fuzzy matching title, desc, and tags)
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.toLowerCase();
      const matchTitle = tool.title.toLowerCase().includes(q);
      const matchDesc = tool.description.toLowerCase().includes(q);
      const matchTags = tool.tags.some(tag => tag.toLowerCase().includes(q));
      return matchTitle || matchDesc || matchTags;
    }
    return true;
  });
});

const currentToolName = computed(() => {
  const match = tools.value.find(t => t.id === currentView.value);
  return match ? match.title : '';
});

const openTool = (tool) => {
  if (tool.status === 'active') {
    currentView.value = tool.id;
  } else {
    alert(`【${tool.title}】正在全力研发中，敬请期待后续版本发布！`);
  }
};

// ==========================================
// 1. LOCAL SPEECH TO TEXT TOOL STATES & METHODS
// ==========================================
const localStt = ref({
  file: null,
  segments: [],
  currentTime: 0,
  isTranscribing: false,
  progressStatus: '准备中...',
  progressDetails: '正在上传并进行预处理...',
  progressPercentage: 0
});

const localMediaPlayer = ref(null);

const onLocalFileSelected = (selectedFile) => {
  localStt.value.file = selectedFile;
  localStt.value.segments = [];
};

const onLocalFileCleared = () => {
  localStt.value.file = null;
  localStt.value.segments = [];
  localStt.value.currentTime = 0;
  localStt.value.isTranscribing = false;
  localStt.value.progressPercentage = 0;
};

const onLocalTimeUpdate = (time) => {
  localStt.value.currentTime = time;
};

const onLocalSeekTo = (seconds) => {
  if (localMediaPlayer.value) {
    localMediaPlayer.value.seekTo(seconds);
  }
};

const onStartLocalTranscription = async ({ file: selectedFile, modelType }) => {
  localStt.value.isTranscribing = true;
  localStt.value.progressPercentage = 0;
  localStt.value.progressStatus = '正在上传音视频文件...';
  localStt.value.progressDetails = '正在建立与本地后端服务的连接，准备上传数据。';

  const xhr = new XMLHttpRequest();
  const formData = new FormData();
  formData.append('file', selectedFile);
  formData.append('modelType', modelType);

  xhr.upload.addEventListener('progress', (e) => {
    if (e.lengthComputable) {
      const percentComplete = Math.round((e.loaded / e.total) * 90);
      localStt.value.progressPercentage = percentComplete;
      localStt.value.progressDetails = `上传进度: ${percentComplete}% (总大小: ${formatBytes(e.total)})`;
      if (percentComplete === 90) {
        localStt.value.progressStatus = '文件上传完毕，后端正在处理...';
        localStt.value.progressDetails = '正在转换音频格式并加载 Whisper 离线模型...';
      }
    }
  });

  let processingInterval = null;
  const startProcessingProgress = () => {
    processingInterval = setInterval(() => {
      if (localStt.value.progressPercentage < 99) {
        localStt.value.progressPercentage += 1;
        if (localStt.value.progressPercentage < 95) {
          localStt.value.progressDetails = '正在将音视频提取并转换为标准 16kHz WAV 格式...';
        } else if (localStt.value.progressPercentage < 98) {
          localStt.value.progressDetails = '正在初始化 Whisper 语音模型 (首次使用需要下载，请限制网速/耐心等待)...';
        } else {
          localStt.value.progressDetails = '正在提取音轨特征并进行智能分词识别...';
        }
      }
    }, 1500);
  };

  xhr.onload = () => {
    clearInterval(processingInterval);
    if (xhr.status >= 200 && xhr.status < 300) {
      try {
        const responseData = JSON.parse(xhr.responseText);
        localStt.value.progressPercentage = 100;
        localStt.value.progressStatus = '识别成功！';
        localStt.value.progressDetails = '文字段落已生成，正在渲染界面...';
        
        setTimeout(() => {
          localStt.value.segments = responseData.segments || responseData.Segments || [];
          localStt.value.isTranscribing = false;
        }, 800);
      } catch (err) {
        handleError('响应数据解析失败', err.message);
      }
    } else {
      let errorMsg = xhr.responseText || `HTTP 错误代码: ${xhr.status}`;
      if (xhr.status === 0) {
        errorMsg = '无法连接到后端 API 服务。请确保后端服务 (ASP.NET Core API) 正在运行。';
      }
      handleError('识别过程出错', errorMsg);
    }
  };

  xhr.onerror = () => {
    clearInterval(processingInterval);
    handleError('连接错误', '网络请求失败，请检查 C# 后端服务是否已正常启动。');
  };

  xhr.open('POST', '/api/transcription/transcribe');
  xhr.send(formData);
  startProcessingProgress();
};

// ==========================================
// 2. ONLINE VIDEO TOOL STATES & METHODS
// ==========================================
const onlineStt = ref({
  serverAudioUrl: '',
  youtubeMetadata: { title: '', avatarUrl: '', platform: 'youtube' },
  segments: [],
  currentTime: 0,
  isTranscribing: false,
  progressStatus: '准备中...',
  progressDetails: '正在解析链接...',
  progressPercentage: 0
});

const onlineMediaPlayer = ref(null);

const onOnlineFileCleared = () => {
  onlineStt.value.serverAudioUrl = '';
  onlineStt.value.youtubeMetadata = { title: '', avatarUrl: '', platform: 'youtube' };
  onlineStt.value.segments = [];
  onlineStt.value.currentTime = 0;
  onlineStt.value.isTranscribing = false;
  onlineStt.value.progressPercentage = 0;
};

const onOnlineTimeUpdate = (time) => {
  onlineStt.value.currentTime = time;
};

const onOnlineSeekTo = (seconds) => {
  if (onlineMediaPlayer.value) {
    onlineMediaPlayer.value.seekTo(seconds);
  }
};

const onStartOnlineTranscription = async ({ url, modelType }) => {
  const isBili = url.toLowerCase().includes('bilibili.com') || url.toLowerCase().includes('b23.tv');
  const platformName = isBili ? 'B站' : 'YouTube';
  
  onlineStt.value.isTranscribing = true;
  onlineStt.value.progressPercentage = 0;
  onlineStt.value.progressStatus = `正在解析 ${platformName} 视频...`;
  onlineStt.value.progressDetails = '获取视频标题、发布者及音轨元数据中...';

  let percent = 0;
  const progressInterval = setInterval(() => {
    if (percent < 90) {
      percent += 2;
      onlineStt.value.progressPercentage = percent;
      if (percent < 25) {
        onlineStt.value.progressStatus = '正在解析链接...';
        onlineStt.value.progressDetails = `获取 ${platformName} 解析协议信息...`;
      } else if (percent < 55) {
        onlineStt.value.progressStatus = '正在下载音频流...';
        onlineStt.value.progressDetails = `正在抓取高质量音频轨道 (进度: ${percent}%)`;
      } else if (percent < 75) {
        onlineStt.value.progressStatus = '正在转换为 16kHz WAV...';
        onlineStt.value.progressDetails = '调用 FFmpeg 对音轨文件进行解码重采样...';
      } else {
        onlineStt.value.progressStatus = '正在进行本地 Whisper 转录...';
        onlineStt.value.progressDetails = '提取音频梅尔特征谱并输入本地离线识别引擎...';
      }
    }
  }, 1000);

  try {
    const response = await fetch('/api/transcription/online/transcribe', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ url, modelType })
    });

    clearInterval(progressInterval);

    if (!response.ok) {
      const errText = await response.text();
      throw new Error(errText || `HTTP 错误代码: ${response.status}`);
    }

    const responseData = await response.json();

    onlineStt.value.progressPercentage = 100;
    onlineStt.value.progressStatus = '解析与识别完成！';
    onlineStt.value.progressDetails = '正在载入音轨流并生成文字段落...';

    setTimeout(() => {
      onlineStt.value.segments = responseData.segments || responseData.Segments || [];
      
      const audioFileName = responseData.fileName || responseData.FileName;
      onlineStt.value.serverAudioUrl = `/api/transcription/audio?filename=${encodeURIComponent(audioFileName)}`;
      
      onlineStt.value.youtubeMetadata = {
        title: responseData.title || responseData.Title || `${platformName} 视频`,
        avatarUrl: responseData.avatarUrl || responseData.AvatarUrl || '',
        platform: isBili ? 'bilibili' : 'youtube'
      };

      onlineStt.value.isTranscribing = false;
    }, 800);

  } catch (error) {
    clearInterval(progressInterval);
    handleError(`${platformName} 识别失败`, error.message);
  }
};

// ==========================================
// COMMON HELPERS
// ==========================================
const handleError = (title, details) => {
  localStt.value.isTranscribing = false;
  onlineStt.value.isTranscribing = false;
  alert(`${title}: ${details}`);
};

const formatBytes = (bytes) => {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
};
</script>

<style>
/* Global Container Layout */
.app-container {
  max-width: 1280px;
  margin: 0 auto;
  padding: 40px 20px;
  display: flex;
  flex-direction: column;
  gap: 32px;
  min-height: 100vh;
}

/* Header */
.app-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 24px;
}

.logo-box {
  display: flex;
  align-items: center;
  gap: 16px;
}

.logo-orb {
  width: 48px;
  height: 48px;
  border-radius: var(--border-radius-md);
  background: var(--accent-gradient);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  box-shadow: 0 4px 20px rgba(139, 92, 246, 0.2);
}

.logo-text h1 {
  font-size: 1.5rem;
  color: var(--text-primary);
  background: linear-gradient(to right, #ffffff, #94a3b8);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
}

.logo-text p {
  font-size: 0.85rem;
  color: var(--text-muted);
  margin-top: 4px;
}

.system-status {
  display: flex;
  gap: 8px;
}

/* Main Grid Layout */
.main-layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 32px;
}

@media (min-width: 1024px) {
  .main-layout {
    grid-template-columns: 420px 1fr;
  }
}

.col-left {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.col-right {
  min-width: 0;
}

/* Media Player Panel Styling */
.player-panel {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.panel-header h3 {
  font-size: 1rem;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.btn-text-action {
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--accent-cyan);
  font-size: 0.85rem;
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.btn-text-action:hover {
  background: rgba(6, 182, 212, 0.08);
  color: #22d3ee;
}

.media-file-info {
  display: flex;
  gap: 8px;
  font-size: 0.85rem;
  border-top: 1px solid var(--border-color);
  padding-top: 12px;
}

.media-file-info .label {
  color: var(--text-muted);
}

.media-file-info .value {
  color: var(--text-secondary);
  font-weight: 500;
  word-break: break-all;
}

/* YouTuber Author Card styling */
.youtube-author-card {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  border-radius: var(--border-radius-md);
  border: 1px solid rgba(255, 255, 255, 0.08);
  margin-top: 8px;
}

.avatar-glow-ring {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  padding: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  flex-shrink: 0;
}

.author-avatar {
  width: 100%;
  height: 100%;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}

.video-info-block {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.video-title-label {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.subtitle-badge {
  font-size: 0.65rem;
  padding: 2px 6px;
  align-self: flex-start;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

/* Empty State Styling */
.empty-transcript-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  height: 520px;
  color: var(--text-secondary);
  padding: 40px;
}

.empty-icon {
  width: 72px;
  height: 72px;
  color: var(--text-muted);
  margin-bottom: 20px;
}

.empty-transcript-state h3 {
  font-size: 1.25rem;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.empty-transcript-state p {
  font-size: 0.9rem;
  color: var(--text-muted);
  max-width: 420px;
  line-height: 1.6;
}
</style>
