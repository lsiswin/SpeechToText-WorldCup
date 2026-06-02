<template>
  <div class="glass-panel transcript-container animate-fade-in">
    <!-- Header Control Area -->
    <div class="transcript-header">
      <div class="view-tabs">
        <button 
          class="tab-btn" 
          :class="{ active: activeView === 'flow' }"
          @click="activeView = 'flow'"
        >
          流式文本
        </button>
        <button 
          class="tab-btn" 
          :class="{ active: activeView === 'timeline' }"
          @click="activeView = 'timeline'"
        >
          时间轴对照
        </button>
      </div>

      <!-- Search Box -->
      <div class="search-box">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor" class="search-icon">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
        <input 
          type="text" 
          placeholder="搜索转录文字..." 
          v-model="searchQuery"
          class="search-input"
        />
        <button v-if="searchQuery" class="clear-search-btn" @click="searchQuery = ''">×</button>
      </div>
    </div>

    <!-- Viewer Body -->
    <div class="transcript-body">
      <!-- 1. Flow Text View -->
      <div v-if="activeView === 'flow'" class="flow-view-container">
        <p class="flow-paragraph">
          <span 
            v-for="seg in processedSegments" 
            :key="seg.index"
            class="flow-segment"
            :class="{ 'active': activeSegmentIndex === seg.index }"
            @click="seekToSegment(seg)"
          >
            <!-- Editable Segment Text -->
            <span 
              v-if="editingIndex !== seg.index" 
              @dblclick="startEditing(seg)"
              v-html="highlightText(seg.text)"
              class="segment-text-span"
              title="双击编辑文字"
            ></span>
            <input 
              v-else 
              ref="editInput" 
              type="text" 
              v-model="editingText" 
              @blur="saveEdit(seg)" 
              @keyup.enter="saveEdit(seg)"
              @keyup.esc="cancelEditing"
              class="inline-edit-input"
            />
          </span>
        </p>
      </div>

      <!-- 2. Timeline List View -->
      <div v-else class="timeline-view-container" ref="timelineContainer">
        <div 
          v-for="seg in processedSegments" 
          :key="seg.index"
          class="timeline-item"
          :class="{ 'active': activeSegmentIndex === seg.index }"
          :data-index="seg.index"
          @click="seekToSegment(seg)"
        >
          <div class="time-tag">
            {{ formatTimeSpan(seg.start) }}
          </div>
          <div class="segment-content">
            <!-- Inline editing -->
            <div v-if="editingIndex !== seg.index" class="text-display" @dblclick="startEditing(seg)" title="双击编辑文字">
              <p v-html="highlightText(seg.text)"></p>
            </div>
            <div v-else class="text-edit-box">
              <input 
                ref="editInput" 
                type="text" 
                v-model="editingText" 
                @blur="saveEdit(seg)" 
                @keyup.enter="saveEdit(seg)"
                @keyup.esc="cancelEditing"
                class="timeline-edit-input"
              />
            </div>
          </div>
          <div class="item-actions">
            <button class="action-btn play-btn" title="从此处播放">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-icon">
                <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM9.555 7.168A1 1 0 008 8v4a1 1 0 001.555.832l3-2a1 1 0 000-1.664l-3-2z" clip-rule="evenodd" />
              </svg>
            </button>
            <button class="action-btn edit-btn" @click.stop="startEditing(seg)" title="编辑文字">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" class="mini-icon">
                <path d="M13.586 3.586a2 2 0 112.828 2.828l-.793.793-2.828-2.828.793-.793zM11.379 5.793L3 14.172V17h2.828l8.38-8.379-2.83-2.828z" />
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Export Footer Area -->
    <div class="transcript-footer">
      <div class="doc-title-input-box">
        <label for="docx-title">Word 导出标题：</label>
        <input 
          type="text" 
          id="docx-title" 
          v-model="exportTitle" 
          placeholder="请输入文档标题" 
          class="export-title-input"
        />
        <label class="checkbox-container">
          <input type="checkbox" v-model="includeTimestamps" />
          <span class="checkmark"></span>
          包含时间戳
        </label>
      </div>

      <div class="export-actions">
        <button class="btn btn-secondary" @click="exportToTxt" title="导出为纯文本 TXT">
          导出 TXT
        </button>
        <button class="btn btn-secondary" @click="exportToSrt" title="导出为视频字幕 SRT">
          导出 SRT 字幕
        </button>
        <button class="btn btn-primary" :disabled="isExporting" @click="exportToWord">
          <span v-if="isExporting" class="spinner"></span>
          <span>导出 Word 文档</span>
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue';

const props = defineProps({
  segments: {
    type: Array,
    required: true
  },
  currentTime: {
    type: Number,
    default: 0
  },
  fileName: {
    type: String,
    default: '语音识别文档'
  }
});

const emit = defineEmits(['seek-to']);

const activeView = ref('flow');
const searchQuery = ref('');
const localSegments = ref([]);
const editingIndex = ref(null);
const editingText = ref('');
const editInput = ref(null);
const timelineContainer = ref(null);

const exportTitle = ref('');
const includeTimestamps = ref(true);
const isExporting = ref(false);

// Initialize localSegments and normalize property naming casing
watch(() => props.segments, (newVal) => {
  localSegments.value = (newVal || []).map(s => ({
    index: s.index !== undefined ? s.index : (s.Index !== undefined ? s.Index : 0),
    start: s.start || s.Start || '00:00:00',
    end: s.end || s.End || '00:00:00',
    text: s.text !== undefined ? s.text : (s.Text !== undefined ? s.Text : '')
  }));
}, { immediate: true, deep: true });

// Setup default export title based on file name
watch(() => props.fileName, (newVal) => {
  if (newVal) {
    const dotIndex = newVal.lastIndexOf('.');
    exportTitle.value = dotIndex !== -1 ? newVal.substring(0, dotIndex) : newVal;
  } else {
    exportTitle.value = '语音转文字结果';
  }
}, { immediate: true });

// Parse TimeSpan string or object to seconds
const timeSpanToSeconds = (timeSpan) => {
  if (typeof timeSpan === 'string') {
    const parts = timeSpan.split(':');
    if (parts.length === 3) {
      const hours = parseInt(parts[0], 10);
      const minutes = parseInt(parts[1], 10);
      const seconds = parseFloat(parts[2]);
      return hours * 3600 + minutes * 60 + seconds;
    }
  } else if (timeSpan && typeof timeSpan === 'object') {
    if ('TotalSeconds' in timeSpan) return timeSpan.TotalSeconds;
  }
  return 0;
};

// Map TimeSpan properties to clean seconds representation
const processedSegments = computed(() => {
  return localSegments.value.map(seg => {
    const startSec = timeSpanToSeconds(seg.start);
    const endSec = timeSpanToSeconds(seg.end);
    return {
      ...seg,
      startSeconds: startSec,
      endSeconds: endSec
    };
  });
});

// Compute active segment index based on player's current time
const activeSegmentIndex = computed(() => {
  const current = props.currentTime;
  const match = processedSegments.value.find(seg => current >= seg.startSeconds && current < seg.endSeconds);
  return match ? match.index : null;
});

// Scroll to active segment in timeline view
watch(activeSegmentIndex, (newVal) => {
  if (activeView.value === 'timeline' && newVal !== null) {
    nextTick(() => {
      const activeEl = timelineContainer.value?.querySelector(`.timeline-item[data-index="${newVal}"]`);
      if (activeEl) {
        activeEl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      }
    });
  }
});

// Formatting utilities
const formatTimeSpan = (timeSpan) => {
  if (typeof timeSpan === 'string') {
    return timeSpan.split('.')[0];
  }
  return '00:00:00';
};

const seekToSegment = (seg) => {
  emit('seek-to', seg.startSeconds);
};

// Text Highlight Filter
const highlightText = (text) => {
  if (!searchQuery.value) return text;
  const query = searchQuery.value.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&');
  const regex = new RegExp(`(${query})`, 'gi');
  return text.replace(regex, '<mark>$1</mark>');
};

// Inline Editing Controls
const startEditing = (seg) => {
  editingIndex.value = seg.index;
  editingText.value = seg.text;
  nextTick(() => {
    if (editInput.value) {
      if (Array.isArray(editInput.value)) {
        editInput.value[0]?.focus();
      } else {
        editInput.value.focus();
      }
    }
  });
};

const saveEdit = (seg) => {
  if (editingIndex.value === null) return;
  const match = localSegments.value.find(s => s.index === seg.index);
  if (match) {
    match.text = editingText.value;
  }
  editingIndex.value = null;
};

const cancelEditing = () => {
  editingIndex.value = null;
};

// Local File Export: TXT
const exportToTxt = () => {
  let content = '';
  localSegments.value.forEach(seg => {
    if (includeTimestamps.value) {
      content += `[${formatTimeSpan(seg.start)} - ${formatTimeSpan(seg.end)}]  `;
    }
    content += `${seg.text}\n`;
  });

  triggerDownload(content, `${exportTitle.value || '转录文件'}.txt`, 'text/plain;charset=utf-8');
};

// Local File Export: SRT Subtitles
const exportToSrt = () => {
  let content = '';
  processedSegments.value.forEach((seg, idx) => {
    content += `${idx + 1}\n`;
    content += `${formatSecondsToSrtTime(seg.startSeconds)} --> ${formatSecondsToSrtTime(seg.endSeconds)}\n`;
    content += `${seg.text}\n\n`;
  });

  triggerDownload(content, `${exportTitle.value || '字幕文件'}.srt`, 'text/srt;charset=utf-8');
};

const formatSecondsToSrtTime = (secondsTotal) => {
  const hours = Math.floor(secondsTotal / 3600);
  const minutes = Math.floor((secondsTotal % 3600) / 60);
  const seconds = Math.floor(secondsTotal % 60);
  const milliseconds = Math.floor((secondsTotal % 1) * 1000);
  
  return `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')},${milliseconds.toString().padStart(3, '0')}`;
};

const triggerDownload = (content, filename, mimeType) => {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
};

// Word Document Exporter through API
const exportToWord = async () => {
  isExporting.value = true;
  try {
    // Map properties back to standard PascalCase which C# backend models define
    const mappedSegments = localSegments.value.map(seg => ({
      Index: seg.index,
      Start: seg.start,
      End: seg.end,
      Text: seg.text
    }));

    const response = await fetch('/api/transcription/export/word', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        title: exportTitle.value || '语音识别转录文档',
        segments: mappedSegments,
        includeTimestamps: includeTimestamps.value
      })
    });

    if (!response.ok) {
      throw new Error(`Server returned error code: ${response.status}`);
    }

    const blob = await response.blob();
    const filename = `${exportTitle.value || '语音识别转录文档'}.docx`;
    
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  } catch (error) {
    console.error('Word export error:', error);
    alert('Word 文档导出失败: ' + error.message);
  } finally {
    isExporting.value = false;
  }
};
</script>

<style scoped>
.transcript-container {
  display: flex;
  flex-direction: column;
  height: 520px;
  background: rgba(15, 21, 36, 0.5);
  overflow: hidden;
}

.transcript-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 16px;
  margin-bottom: 16px;
  gap: 16px;
  flex-wrap: wrap;
}

.view-tabs {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 4px;
  display: inline-flex;
}

.tab-btn {
  background: transparent;
  border: none;
  padding: 8px 16px;
  font-size: 0.875rem;
  color: var(--text-secondary);
  border-radius: 6px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  color: var(--text-primary);
}

.tab-btn.active {
  background: var(--bg-tertiary);
  color: var(--accent-cyan);
  box-shadow: var(--shadow-sm);
}

.search-box {
  display: flex;
  align-items: center;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius-md);
  padding: 4px 12px;
  position: relative;
  width: 240px;
  transition: border-color 0.2s ease;
}

.search-box:focus-within {
  border-color: var(--accent-cyan);
}

.search-icon {
  width: 16px;
  height: 16px;
  color: var(--text-muted);
  margin-right: 8px;
}

.search-input {
  background: transparent;
  border: none;
  outline: none;
  color: var(--text-primary);
  font-size: 0.875rem;
  width: 100%;
}

.clear-search-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  font-size: 1.1rem;
  padding: 0 4px;
}

.clear-search-btn:hover {
  color: var(--text-primary);
}

/* Viewer Body */
.transcript-body {
  flex-grow: 1;
  overflow-y: auto;
  padding-right: 8px;
  margin-bottom: 20px;
}

/* Flow View */
.flow-view-container {
  line-height: 1.8;
  font-size: 1rem;
  color: var(--text-secondary);
  text-align: justify;
}

.flow-segment {
  cursor: pointer;
  padding: 2px 4px;
  border-radius: 4px;
  transition: all 0.2s ease;
  display: inline;
}

.flow-segment:hover {
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-primary);
}

.flow-segment.active {
  background: rgba(6, 182, 212, 0.12);
  color: var(--accent-cyan);
  border-bottom: 1px dashed rgba(6, 182, 212, 0.4);
}

.inline-edit-input {
  background: var(--bg-tertiary);
  border: 1px solid var(--accent-cyan);
  border-radius: 4px;
  color: var(--text-primary);
  padding: 2px 6px;
  outline: none;
  font-size: 0.95rem;
  width: 250px;
}

/* Timeline View */
.timeline-view-container {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.timeline-item {
  display: flex;
  align-items: flex-start;
  padding: 12px;
  border-radius: var(--border-radius-md);
  border: 1px solid transparent;
  background: rgba(255, 255, 255, 0.01);
  transition: all 0.25s ease;
  cursor: pointer;
  gap: 16px;
}

.timeline-item:hover {
  background: rgba(255, 255, 255, 0.03);
  border-color: var(--border-color);
}

.timeline-item.active {
  background: rgba(139, 92, 246, 0.05);
  border-color: rgba(139, 92, 246, 0.2);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
}

.time-tag {
  font-family: var(--font-mono);
  font-size: 0.8rem;
  color: var(--accent-cyan);
  background: rgba(6, 182, 212, 0.1);
  padding: 2px 8px;
  border-radius: 4px;
  white-space: nowrap;
  margin-top: 2px;
}

.timeline-item.active .time-tag {
  background: var(--accent-gradient);
  color: var(--text-primary);
}

.segment-content {
  flex-grow: 1;
  font-size: 0.95rem;
  line-height: 1.6;
  color: var(--text-secondary);
}

.timeline-item.active .segment-content {
  color: var(--text-primary);
}

.timeline-edit-input {
  background: var(--bg-tertiary);
  border: 1px solid var(--accent-purple);
  border-radius: 4px;
  color: var(--text-primary);
  padding: 4px 8px;
  width: 100%;
  outline: none;
}

.item-actions {
  display: flex;
  gap: 4px;
  opacity: 0;
  transition: opacity 0.2s ease;
  flex-shrink: 0;
}

.timeline-item:hover .item-actions {
  opacity: 1;
}

.action-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
}

.action-btn:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
}

.play-btn:hover {
  color: var(--accent-cyan);
}

.edit-btn:hover {
  color: var(--accent-purple);
}

.mini-icon {
  width: 16px;
  height: 16px;
}

/* Transcript Footer */
.transcript-footer {
  border-top: 1px solid var(--border-color);
  padding-top: 16px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  flex-wrap: wrap;
}

.doc-title-input-box {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-grow: 1;
  min-width: 280px;
}

.doc-title-input-box label {
  font-size: 0.85rem;
  color: var(--text-secondary);
  white-space: nowrap;
}

.export-title-input {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  color: var(--text-primary);
  padding: 8px 12px;
  outline: none;
  font-size: 0.875rem;
  flex-grow: 1;
  transition: border-color 0.2s ease;
}

.export-title-input:focus {
  border-color: var(--accent-purple);
}

/* Custom Checkbox */
.checkbox-container {
  display: flex;
  align-items: center;
  position: relative;
  padding-left: 24px;
  cursor: pointer;
  font-size: 0.85rem;
  color: var(--text-secondary);
  user-select: none;
  white-space: nowrap;
}

.checkbox-container input {
  position: absolute;
  opacity: 0;
  cursor: pointer;
  height: 0;
  width: 0;
}

.checkmark {
  position: absolute;
  top: 50%;
  left: 0;
  transform: translateY(-50%);
  height: 16px;
  width: 16px;
  background-color: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  transition: all 0.2s ease;
}

.checkbox-container:hover input ~ .checkmark {
  border-color: var(--text-muted);
}

.checkbox-container input:checked ~ .checkmark {
  background-color: var(--accent-cyan);
  border-color: var(--accent-cyan);
}

.checkmark:after {
  content: "";
  position: absolute;
  display: none;
}

.checkbox-container input:checked ~ .checkmark:after {
  display: block;
}

.checkbox-container .checkmark:after {
  left: 5px;
  top: 2px;
  width: 4px;
  height: 8px;
  border: solid white;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.export-actions {
  display: flex;
  gap: 12px;
  flex-shrink: 0;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
