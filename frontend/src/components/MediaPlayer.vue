<template>
  <div class="glass-panel media-player-container" v-if="fileUrl">
    <div class="player-wrapper">
      <!-- Video Element -->
      <video 
        v-if="isVideo" 
        ref="videoPlayer" 
        :src="fileUrl" 
        controls 
        class="video-element"
        @timeupdate="onTimeUpdate"
        @loadedmetadata="onLoadedMetadata"
      ></video>

      <!-- Audio Element -->
      <div v-else class="audio-player-ui">
        <audio 
          ref="audioPlayer" 
          :src="fileUrl" 
          class="audio-element-hidden"
          @timeupdate="onTimeUpdate"
          @loadedmetadata="onLoadedMetadata"
          @play="isPlaying = true"
          @pause="isPlaying = false"
        ></audio>

        <div class="audio-visual-box">
          <div class="disc-animation" :class="{ 'rotating': isPlaying }">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="disc-icon">
              <path fill-rule="evenodd" d="M12 2.25c-5.385 0-9.75 4.365-9.75 9.75s4.365 9.75 9.75 9.75 9.75-4.365 9.75-9.75S17.385 2.25 12 2.25zM12.75 9a.75.75 0 00-1.5 0v3.75a.75.75 0 00.75.75h3.75a.75.75 0 000-1.5h-3V9z" clip-rule="evenodd" />
            </svg>
          </div>
          <div class="wave-spectrogram" v-if="isPlaying">
            <div class="bar" v-for="i in 15" :key="i" :style="{ 
              animationDelay: (i * 0.08) + 's',
              height: Math.floor(Math.random() * 24 + 8) + 'px'
            }"></div>
          </div>
          <div class="wave-spectrogram static" v-else>
            <div class="bar" v-for="i in 15" :key="i" style="height: 6px;"></div>
          </div>
        </div>

        <div class="audio-controls">
          <button class="control-btn play-pause-btn" @click="togglePlay" :title="isPlaying ? '暂停' : '播放'">
            <svg v-if="!isPlaying" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="play-icon">
              <path fill-rule="evenodd" d="M4.5 5.653c0-1.426 1.529-2.33 2.779-1.643l11.54 6.348c1.295.712 1.295 2.573 0 3.285L7.28 20.03c-1.25.687-2.779-.217-2.779-1.643V5.653z" clip-rule="evenodd" />
            </svg>
            <svg v-else xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" class="pause-icon">
              <path fill-rule="evenodd" d="M6.75 5.25a.75.75 0 01.75-.75H9a.75.75 0 01.75.75v13.5a.75.75 0 01-.75.75H7.5a.75.75 0 01-.75-.75V5.25zm7.5 0A.75.75 0 0115 4.5h1.5a.75.75 0 01.75.75v13.5a.75.75 0 01-.75.75H15a.75.75 0 01-.75-.75V5.25z" clip-rule="evenodd" />
            </svg>
          </button>

          <!-- Timeline Slider -->
          <div class="timeline-container">
            <span class="time-label">{{ formatTime(currentTime) }}</span>
            <input 
              type="range" 
              class="timeline-slider"
              min="0" 
              :max="duration || 100" 
              step="0.1" 
              v-model="currentTime" 
              @input="onSeek"
            />
            <span class="time-label">{{ formatTime(duration) }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';

const props = defineProps({
  file: {
    type: Object,
    default: null
  },
  srcUrl: {
    type: String,
    default: ''
  }
});

const emit = defineEmits(['time-update']);

const videoPlayer = ref(null);
const audioPlayer = ref(null);
const isPlaying = ref(false);
const currentTime = ref(0);
const duration = ref(0);

const isVideo = computed(() => {
  if (props.file) {
    return props.file.type.startsWith('video');
  }
  return false; // Remotely streamed audio files from YouTube are not video elements
});

const fileUrl = computed(() => {
  if (props.file) {
    return URL.createObjectURL(props.file);
  }
  return props.srcUrl;
});

// Sync play/pause
const togglePlay = () => {
  const player = isVideo.value ? videoPlayer.value : audioPlayer.value;
  if (!player) return;

  if (isPlaying.value) {
    player.pause();
    isPlaying.value = false;
  } else {
    player.play();
    isPlaying.value = true;
  }
};

const onTimeUpdate = (e) => {
  const player = e.target;
  currentTime.value = player.currentTime;
  emit('time-update', player.currentTime);
};

const onLoadedMetadata = (e) => {
  duration.value = e.target.duration;
};

const onSeek = () => {
  const player = isVideo.value ? videoPlayer.value : audioPlayer.value;
  if (player) {
    player.currentTime = currentTime.value;
  }
};

// Method to seek dynamically from parent
const seekTo = (seconds) => {
  const player = isVideo.value ? videoPlayer.value : audioPlayer.value;
  if (player) {
    player.currentTime = seconds;
    currentTime.value = seconds;
    player.play();
    isPlaying.value = true;
  }
};

// Define expose to allow parent calling seekTo
defineExpose({
  seekTo
});

const formatTime = (secs) => {
  if (isNaN(secs)) return '00:00';
  const m = Math.floor(secs / 60);
  const s = Math.floor(secs % 60);
  return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
};
</script>

<style scoped>
.media-player-container {
  padding: 16px;
  background: rgba(15, 21, 36, 0.4);
}

.player-wrapper {
  width: 100%;
  display: flex;
  flex-direction: column;
}

.video-element {
  width: 100%;
  max-height: 360px;
  border-radius: var(--border-radius-md);
  background: black;
  outline: none;
}

/* Audio Player */
.audio-player-ui {
  display: flex;
  align-items: center;
  gap: 20px;
  width: 100%;
  flex-wrap: wrap;
}

.audio-element-hidden {
  display: none;
}

.audio-visual-box {
  display: flex;
  align-items: center;
  gap: 16px;
  background: rgba(255, 255, 255, 0.02);
  padding: 8px 16px;
  border-radius: var(--border-radius-md);
  border: 1px solid var(--border-color);
  flex-shrink: 0;
}

.disc-animation {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  background: var(--accent-gradient);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-primary);
}

.disc-animation.rotating {
  animation: rotateDisc 4s linear infinite;
}

.disc-icon {
  width: 20px;
  height: 20px;
}

@keyframes rotateDisc {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.wave-spectrogram {
  display: flex;
  align-items: center;
  gap: 3px;
  height: 36px;
  width: 120px;
}

.wave-spectrogram .bar {
  width: 3px;
  background: var(--accent-cyan);
  border-radius: 1.5px;
  animation: audioWave 1s ease-in-out infinite alternate;
}

.wave-spectrogram.static .bar {
  background: var(--text-muted);
}

@keyframes audioWave {
  0% { transform: scaleY(0.3); }
  100% { transform: scaleY(1.2); }
}

.audio-controls {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-grow: 1;
  min-width: 240px;
}

.control-btn {
  width: 44px;
  height: 44px;
  border-radius: 50%;
  background: var(--accent-gradient);
  color: white;
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 10px rgba(139, 92, 246, 0.2);
  transition: all 0.2s ease;
  flex-shrink: 0;
}

.control-btn:hover {
  transform: scale(1.05);
  box-shadow: 0 4px 15px rgba(6, 182, 212, 0.3);
}

.play-icon, .pause-icon {
  width: 20px;
  height: 20px;
}

.timeline-container {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-grow: 1;
}

.time-label {
  font-family: var(--font-mono);
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.timeline-slider {
  flex-grow: 1;
  -webkit-appearance: none;
  appearance: none;
  background: var(--bg-tertiary);
  height: 4px;
  border-radius: 2px;
  outline: none;
  cursor: pointer;
}

.timeline-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--accent-cyan);
  box-shadow: 0 0 5px rgba(6, 182, 212, 0.8);
  cursor: pointer;
  transition: transform 0.1s ease;
}

.timeline-slider::-webkit-slider-thumb:hover {
  transform: scale(1.3);
}
</style>
