<template>
  <div class="world-cup-predictor-container">
    <!-- Header Banner -->
    <div class="predictor-header glass-panel">
      <div class="header-left">
        <span class="badge badge-purple">2026 FIFA WORLD CUP</span>
        <h2>美加墨世界杯晋级预测</h2>
        <p>预测首届 48 支球队扩军赛制，体验全新的 12 个小组和 32 强淘汰赛对决！</p>
      </div>
      <div class="header-actions">
        <button class="btn btn-secondary" @click="resetAll" :disabled="isSimulating">
          🔄 重置预测
        </button>
        <button class="btn btn-primary simulator-btn" @click="autoSimulateAll" :disabled="isSimulating">
          <span class="btn-glow-orb"></span>
          {{ isGlobalSimulating ? '🤖 AI 正在火速推演中...' : '🤖 一键 AI 模拟所有对局 (DeepSeek)' }}
        </button>
      </div>
    </div>

    <!-- Phase Tabs Navigation -->
    <div class="phase-navigation-bar glass-panel">
      <button 
        v-for="phase in phases" 
        :key="phase.id"
        class="phase-nav-btn"
        :class="{ active: currentPhase === phase.id, completed: isPhaseCompleted(phase.id) }"
        @click="switchPhase(phase.id)"
        :disabled="!isPhaseAccessible(phase.id)"
      >
        <span class="phase-icon">{{ phase.icon }}</span>
        <span class="phase-name">{{ phase.name }}</span>
        <span class="status-badge" v-if="isPhaseCompleted(phase.id)">✓</span>
      </button>
    </div>

    <!-- Phase Content Switcher -->
    <div class="phase-content-container">
      
      <!-- PHASE 1: GROUP STAGE -->
      <div v-if="currentPhase === 'group'" class="group-stage-view animate-fade-in">
        <div class="stage-section-header">
          <h3>第一阶段：小组赛积分预测 (12 个小组)</h3>
          <span class="section-desc">您可以通过展开赛程并输入比分进行精准预测，系统将实时进行蒙特卡洛模拟，为您展示各队的实时晋级概率。</span>
        </div>

        <div class="groups-grid">
          <div v-for="group in groups" :key="group.id" class="group-card glass-panel">
            <div class="group-card-header">
              <h4>{{ group.name }} 组</h4>
              <button class="btn-mini-action" @click="simulateGroup(group.id)" :disabled="isSimulating">
                {{ simulatingGroupId === group.id ? '🤖 AI 正在火速推演中...' : '⚡ AI 模拟本组' }}
              </button>
            </div>

            <!-- Standings Table -->
            <div class="standings-table-wrapper">
              <table class="standings-table">
                <thead>
                  <tr>
                    <th class="col-rank">#</th>
                    <th class="col-team">球队</th>
                    <th class="col-stat" title="已赛场次">场次</th>
                    <th class="col-stat" title="净胜球">净胜</th>
                    <th class="col-stat" title="积分">积分</th>
                    <th class="col-order">晋级状态</th>
                  </tr>
                </thead>
                <tbody>
                  <tr 
                    v-for="team in group.standings" 
                    :key="team.id"
                    :class="{ 
                      'qualifies-direct': getTeamRankInGroup(group, team.id) <= 2, 
                      'qualifies-third': getTeamRankInGroup(group, team.id) === 3 
                    }"
                  >
                    <td class="col-rank">
                      <span class="rank-number" :class="'rank-' + getTeamRankInGroup(group, team.id)">{{ getTeamRankInGroup(group, team.id) }}</span>
                    </td>
                    <td class="col-team">
                      <img :src="'https://flagcdn.com/w40/' + team.code + '.png'" class="team-flag-img" :alt="team.name" />
                      <span class="team-name-text">{{ team.name }}</span>
                      <span class="host-badge" v-if="team.isHost">主</span>
                      <span class="champ-badge" v-if="team.isChampion">卫</span>
                    </td>
                    <td class="col-stat">{{ team.gp }}</td>
                    <td class="col-stat" :class="{ 'positive-gd': team.gd > 0, 'negative-gd': team.gd < 0 }">
                      {{ team.gd > 0 ? '+' + team.gd : team.gd }}
                    </td>
                    <td class="col-stat font-weight-bold">{{ team.pts }}</td>
                    <td class="col-order text-center">
                      <span v-if="teamProbabilities[team.id] === 100" class="status-indicator-badge pass">已晋级</span>
                      <span v-else-if="teamProbabilities[team.id] === 0" class="status-indicator-badge fail">已淘汰</span>
                      <span v-else class="probability-badge">{{ teamProbabilities[team.id] }}%</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Mode Toggle Button -->
            <div class="group-mode-actions">
              <button 
                class="btn-text-only" 
                @click="toggleGroupMatches(group.id)"
              >
                {{ group.showMatches ? '收起比赛列表 ▲' : '进行比分预测 (展开赛程) ▼' }}
              </button>
            </div>

            <!-- Expandable Match Predictor Drawer -->
            <transition name="slide-drawer">
              <div v-if="group.showMatches" class="group-matches-drawer">
                <div v-for="(match, idx) in group.matches" :key="idx" class="match-row">
                  <div class="match-date-badge">{{ match.date }}</div>
                  
                  <div class="match-team home">
                    <span class="team-name">{{ getTeam(match.homeId).name }}</span>
                    <img :src="'https://flagcdn.com/w40/' + getTeam(match.homeId).code + '.png'" class="team-flag-img" :alt="getTeam(match.homeId).name" />
                  </div>
                  
                  <div class="match-score-inputs">
                    <input 
                      type="number" 
                      min="0" 
                      max="20"
                      placeholder="-"
                      v-model.number="match.homeScore" 
                      @input="onScoreChanged(group.id)"
                      class="score-input"
                    />
                    <span class="score-vs">:</span>
                    <input 
                      type="number" 
                      min="0" 
                      max="20"
                      placeholder="-"
                      v-model.number="match.awayScore" 
                      @input="onScoreChanged(group.id)"
                      class="score-input"
                    />
                  </div>

                  <div class="match-team away">
                    <img :src="'https://flagcdn.com/w40/' + getTeam(match.awayId).code + '.png'" class="team-flag-img" :alt="getTeam(match.awayId).name" />
                    <span class="team-name">{{ getTeam(match.awayId).name }}</span>
                  </div>
                </div>
              </div>
            </transition>
          </div>
        </div>

        <div class="stage-footer-actions">
          <button class="btn btn-primary btn-lg" @click="proceedToThirdPlace">
            下一步：决定最佳小组第三名 &rarr;
          </button>
        </div>
      </div>

      <!-- PHASE 2: THIRD PLACE COMPARISON -->
      <div v-else-if="currentPhase === 'third'" class="third-place-view animate-fade-in">
        <div class="stage-section-header">
          <h3>第二阶段：小组第三对比与筛选</h3>
          <span class="section-desc">12 个小组第 3 名进行横向对比，积分、净胜球、进球数占优的前 8 支球队将获得 32 强资格。</span>
        </div>

        <div class="third-place-layout">
          <!-- Comparison Card -->
          <div class="third-comparison-card glass-panel">
            <div class="card-inner-header">
              <h4>小组第三排行榜 (12 选 8)</h4>
              <span class="badge badge-success">前 8 名出线</span>
            </div>

            <table class="standings-table comparison-table">
              <thead>
                <tr>
                  <th class="col-rank">#</th>
                  <th class="col-group">小组</th>
                  <th class="col-team">球队</th>
                  <th class="col-stat">场次</th>
                  <th class="col-stat">净胜</th>
                  <th class="col-stat">进球</th>
                  <th class="col-stat">积分</th>
                  <th class="col-status">晋级状态</th>
                  <th class="col-manual-toggle">强制出线</th>
                </tr>
              </thead>
              <tbody>
                <tr 
                  v-for="(item, index) in thirdPlaceList" 
                  :key="item.team.id"
                  :class="{ 'third-qualified': isThirdQualified(item.team.id), 'third-eliminated': !isThirdQualified(item.team.id) }"
                >
                  <td class="col-rank">
                    <span class="rank-number" :class="index < 8 ? 'rank-1' : 'rank-4'">{{ index + 1 }}</span>
                  </td>
                  <td class="col-group">{{ item.groupId }} 组</td>
                  <td class="col-team">
                    <img :src="'https://flagcdn.com/w40/' + item.team.code + '.png'" class="team-flag-img" :alt="item.team.name" />
                    <span class="team-name-text">{{ item.team.name }}</span>
                  </td>
                  <td class="col-stat">{{ item.team.gp }}</td>
                  <td class="col-stat" :class="{ 'positive-gd': item.team.gd > 0, 'negative-gd': item.team.gd < 0 }">
                    {{ item.team.gd > 0 ? '+' + item.team.gd : item.team.gd }}
                  </td>
                  <td class="col-stat">{{ item.team.gs }}</td>
                  <td class="col-stat font-weight-bold">{{ item.team.pts }}</td>
                  <td class="col-status">
                    <span class="status-indicator-badge" :class="isThirdQualified(item.team.id) ? 'pass' : 'fail'">
                      {{ isThirdQualified(item.team.id) ? '已出线' : '已淘汰' }}
                    </span>
                  </td>
                  <td class="col-manual-toggle">
                    <input 
                      type="checkbox" 
                      :checked="isThirdQualified(item.team.id)"
                      @change="toggleManualThirdQualify(item.team.id)"
                      class="custom-checkbox"
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <!-- Help Info Card -->
          <div class="third-info-card glass-panel">
            <h4>💡 扩军赛制科普</h4>
            <p>由于美加墨世界杯扩军至 48 支球队，原先 8 个小组直接出线 16 强的赛制已作废。现行赛制为：</p>
            <ul>
              <li><strong>12 个小组</strong>各进行 6 场单循环赛。</li>
              <li>每组的 <strong>第一名</strong> 和 <strong>第二名</strong> 直接挺进 32 强（共 24 支队伍）。</li>
              <li>所有 12 个小组第 3 名进行积分大排行。</li>
              <li><strong>积分前 8 名</strong> 的小组第 3 将拿到最后 8 张淘汰赛门票。</li>
            </ul>
            <div class="selection-counter-card">
              <span class="counter-label">当前选择出线人数：</span>
              <span class="counter-value" :class="{ 'correct': manualQualifiedThirds.length === 8 }">
                {{ manualQualifiedThirds.length }} / 8
              </span>
            </div>
            <p class="warning-text-hint" v-if="manualQualifiedThirds.length !== 8">
              ⚠️ 请强制调整或保持勾选正好 8 支球队，以生成 32 强对阵。
            </p>
          </div>
        </div>

        <div class="stage-footer-actions">
          <button class="btn btn-secondary" @click="currentPhase = 'group'">&larr; 返回小组赛修改</button>
          <button 
            class="btn btn-primary btn-lg" 
            :disabled="manualQualifiedThirds.length !== 8" 
            @click="generateKnockout"
          >
            下一步：生成 32 强淘汰赛对局 &rarr;
          </button>
        </div>
      </div>

      <!-- PHASE 3: KNOCKOUT BRACKET -->
      <div v-else-if="currentPhase === 'knockout'" class="knockout-stage-view animate-fade-in">
        <div class="stage-section-header">
          <h3>第三阶段：淘汰赛对阵图 (点击球队即晋级)</h3>
          <span class="section-desc">本届世界杯 32 强对阵按照国际足联规定落位。点击您预测胜出的国家队即可推进晋级。</span>
        </div>

        <div class="bracket-navigation-controls">
          <button class="btn-mini-action" @click="autoSimulateKnockout" :disabled="isSimulating">
            🤖 AI 模拟剩余淘汰赛
          </button>
        </div>

        <!-- Scrollable Bracket Grid Area -->
        <div class="bracket-viewport">
          <div class="bracket-canvas">
            
            <!-- LEFT HALF BRACKET (Matches 1-8) -->
            <div class="bracket-half left-half">
              <!-- Round of 32 -->
              <div class="bracket-round r-32">
                <div class="round-title">1/16 决赛 (左半区)</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in leftR32" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ 
                        winner: match.winner === 'team1', 
                        loser: match.winner === 'team2',
                        highlighted: hoveredTeamId === match.team1?.id
                      }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name" v-if="match.team1">{{ match.team1.name }}</span>
                      <span class="team-name color-muted" v-else>待定</span>
                      <span class="seed-text" v-if="match.team1">{{ match.team1Seed }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ 
                        winner: match.winner === 'team2', 
                        loser: match.winner === 'team1',
                        highlighted: hoveredTeamId === match.team2?.id
                      }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name" v-if="match.team2">{{ match.team2.name }}</span>
                      <span class="team-name color-muted" v-else>待定</span>
                      <span class="seed-text" v-if="match.team2">{{ match.team2Seed }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Round of 16 -->
              <div class="bracket-round r-16">
                <div class="round-title">1/8 决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in leftR16" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Quarterfinals -->
              <div class="bracket-round r-qf">
                <div class="round-title">1/4 决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in leftQf" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Semifinals -->
              <div class="bracket-round r-sf">
                <div class="round-title">半决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in leftSf" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <!-- CENTRAL STAGE: FINAL & CHAMPION AREA -->
            <div class="bracket-center-stage">
              <div class="final-container">
                <div class="trophy-glow-bg">🏆</div>
                
                <div class="match-title-center">GRAND FINAL · 总决赛</div>
                
                <!-- Final Match Card -->
                <div class="final-match-card glass-panel" v-if="finalMatch">
                  <div 
                    class="final-team-row home"
                    :class="{ 
                      winner: finalMatch.winner === 'team1', 
                      loser: finalMatch.winner === 'team2',
                      highlighted: hoveredTeamId === finalMatch.team1?.id
                    }"
                    @mouseenter="hoveredTeamId = finalMatch.team1?.id"
                    @mouseleave="hoveredTeamId = null"
                    @click="advanceTeam(finalMatch.id, 'team1')"
                  >
                    <img v-if="finalMatch.team1" :src="'https://flagcdn.com/w160/' + finalMatch.team1.code + '.png'" class="team-flag-img-huge" :alt="finalMatch.team1.name" />
                    <span class="team-name-huge" v-if="finalMatch.team1">{{ finalMatch.team1.name }}</span>
                    <span class="team-name-huge color-muted" v-else>待定</span>
                    <span class="crown-icon" v-if="finalMatch.winner === 'team1'">👑</span>
                  </div>

                  <div class="final-vs-divider">VS</div>

                  <div 
                    class="final-team-row away"
                    :class="{ 
                      winner: finalMatch.winner === 'team2', 
                      loser: finalMatch.winner === 'team1',
                      highlighted: hoveredTeamId === finalMatch.team2?.id
                    }"
                    @mouseenter="hoveredTeamId = finalMatch.team2?.id"
                    @mouseleave="hoveredTeamId = null"
                    @click="advanceTeam(finalMatch.id, 'team2')"
                  >
                    <img v-if="finalMatch.team2" :src="'https://flagcdn.com/w160/' + finalMatch.team2.code + '.png'" class="team-flag-img-huge" :alt="finalMatch.team2.name" />
                    <span class="team-name-huge" v-if="finalMatch.team2">{{ finalMatch.team2.name }}</span>
                    <span class="team-name-huge color-muted" v-else>待定</span>
                    <span class="crown-icon" v-if="finalMatch.winner === 'team2'">👑</span>
                  </div>
                </div>

                <!-- 3rd Place Match Card -->
                <div class="third-place-playoff-box" v-if="thirdPlaceMatch">
                  <div class="third-match-title">季军争夺战</div>
                  <div class="third-playoff-card">
                    <div 
                      class="third-team-slot"
                      :class="{ winner: thirdPlaceMatch.winner === 'team1', highlighted: hoveredTeamId === thirdPlaceMatch.team1?.id }"
                      @mouseenter="hoveredTeamId = thirdPlaceMatch.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(thirdPlaceMatch.id, 'team1')"
                    >
                      <img v-if="thirdPlaceMatch.team1" :src="'https://flagcdn.com/w40/' + thirdPlaceMatch.team1.code + '.png'" class="team-flag-img inline-flag" :alt="thirdPlaceMatch.team1.name" />
                      <span>{{ thirdPlaceMatch.team1?.name || '待定' }}</span>
                    </div>
                    <span class="vs">vs</span>
                    <div 
                      class="third-team-slot"
                      :class="{ winner: thirdPlaceMatch.winner === 'team2', highlighted: hoveredTeamId === thirdPlaceMatch.team2?.id }"
                      @mouseenter="hoveredTeamId = thirdPlaceMatch.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(thirdPlaceMatch.id, 'team2')"
                    >
                      <img v-if="thirdPlaceMatch.team2" :src="'https://flagcdn.com/w40/' + thirdPlaceMatch.team2.code + '.png'" class="team-flag-img inline-flag" :alt="thirdPlaceMatch.team2.name" />
                      <span>{{ thirdPlaceMatch.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>

                <!-- Champion Showcase Display -->
                <transition name="pop-champion">
                  <div class="champion-announcement glass-panel" v-if="championTeam">
                    <span class="celebration-ribbon">🏆 2026 世界杯总冠军 🏆</span>
                    <img :src="'https://flagcdn.com/w160/' + championTeam.code + '.png'" class="team-flag-img-huge block-center" :alt="championTeam.name" />
                    <h3>{{ championTeam.name }}</h3>
                    <p>实力评级值: {{ championTeam.strength }} | 预测登顶美加墨之巅</p>
                    <button class="btn btn-primary" @click="currentPhase = 'report'">
                      📊 查看完整预测报告
                    </button>
                  </div>
                </transition>
              </div>
            </div>

            <!-- RIGHT HALF BRACKET (Matches 9-16) -->
            <div class="bracket-half right-half">
              <!-- Semifinals -->
              <div class="bracket-round r-sf">
                <div class="round-title">半决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in rightSf" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Quarterfinals -->
              <div class="bracket-round r-qf">
                <div class="round-title">1/4 决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in rightQf" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Round of 16 -->
              <div class="bracket-round r-16">
                <div class="round-title">1/8 决赛</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in rightR16" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team1', loser: match.winner === 'team2', highlighted: hoveredTeamId === match.team1?.id }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name">{{ match.team1?.name || '待定' }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ winner: match.winner === 'team2', loser: match.winner === 'team1', highlighted: hoveredTeamId === match.team2?.id }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name">{{ match.team2?.name || '待定' }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Round of 32 -->
              <div class="bracket-round r-32">
                <div class="round-title">1/16 决赛 (右半区)</div>
                <div class="match-cards-list">
                  <div 
                    v-for="match in rightR32" 
                    :key="match.id" 
                    class="bracket-match-card"
                  >
                    <div 
                      class="team-slot" 
                      :class="{ 
                        winner: match.winner === 'team1', 
                        loser: match.winner === 'team2',
                        highlighted: hoveredTeamId === match.team1?.id
                      }"
                      @mouseenter="hoveredTeamId = match.team1?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team1')"
                    >
                      <img v-if="match.team1" :src="'https://flagcdn.com/w40/' + match.team1.code + '.png'" class="team-flag-img" :alt="match.team1.name" />
                      <span class="team-name" v-if="match.team1">{{ match.team1.name }}</span>
                      <span class="team-name color-muted" v-else>待定</span>
                      <span class="seed-text" v-if="match.team1">{{ match.team1Seed }}</span>
                    </div>
                    <div 
                      class="team-slot" 
                      :class="{ 
                        winner: match.winner === 'team2', 
                        loser: match.winner === 'team1',
                        highlighted: hoveredTeamId === match.team2?.id
                      }"
                      @mouseenter="hoveredTeamId = match.team2?.id"
                      @mouseleave="hoveredTeamId = null"
                      @click="advanceTeam(match.id, 'team2')"
                    >
                      <img v-if="match.team2" :src="'https://flagcdn.com/w40/' + match.team2.code + '.png'" class="team-flag-img" :alt="match.team2.name" />
                      <span class="team-name" v-if="match.team2">{{ match.team2.name }}</span>
                      <span class="team-name color-muted" v-else>待定</span>
                      <span class="seed-text" v-if="match.team2">{{ match.team2Seed }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

          </div>
        </div>

        <div class="stage-footer-actions">
          <button class="btn btn-secondary" @click="currentPhase = 'third'">&larr; 返回小组第三选择</button>
        </div>
      </div>

      <!-- PHASE 4: FINAL REPORT -->
      <div v-else-if="currentPhase === 'report'" class="final-report-view animate-fade-in">
        <div class="stage-section-header">
          <h3>第四阶段：您的世界杯预测报告</h3>
          <span class="section-desc">预测结果已自动归档并保存在您的本地浏览器中。</span>
        </div>

        <div class="report-grid">
          <!-- Left: Big Champion Banner -->
          <div class="report-champion-card glass-panel text-center">
            <span class="stars-decorator">⭐⭐⭐⭐⭐</span>
            <h4 class="spacing-bottom">👑 世界杯总冠军 👑</h4>
            <img v-if="championTeam" :src="'https://flagcdn.com/w160/' + championTeam.code + '.png'" class="team-flag-img-huge block-center margin-auto" :alt="championTeam.name" />
            <h2 class="winner-title-text" v-if="championTeam">{{ championTeam.name }}</h2>
            <p class="winner-subtitle-text" v-if="championTeam">预测在 2026 年 7 月美加墨夏夜捧起金杯</p>
            <div class="runner-up-box" v-if="runnerUpTeam">
              <span class="label">亚军：</span>
              <img :src="'https://flagcdn.com/w40/' + runnerUpTeam.code + '.png'" class="team-flag-img vertical-middle" :alt="runnerUpTeam.name" />
              <span class="value">{{ runnerUpTeam.name }}</span>
            </div>
            <div class="runner-up-box" v-if="thirdPlaceWinnerTeam">
              <span class="label">季军：</span>
              <img :src="'https://flagcdn.com/w40/' + thirdPlaceWinnerTeam.code + '.png'" class="team-flag-img vertical-middle" :alt="thirdPlaceWinnerTeam.name" />
              <span class="value">{{ thirdPlaceWinnerTeam.name }}</span>
            </div>
          </div>

          <!-- Right: Summary details -->
          <div class="report-details-card glass-panel">
            <h4>📊 晋级路线总结</h4>
            
            <div class="road-step">
              <span class="step-badge">4 强席位</span>
              <div class="team-tags-list">
                <span v-for="team in semifinalists" :key="team.id" class="mini-team-tag">
                  <img :src="'https://flagcdn.com/w40/' + team.code + '.png'" class="team-flag-img vertical-middle" :alt="team.name" />
                  <span class="vertical-middle">{{ team.name }}</span>
                </span>
              </div>
            </div>

            <div class="road-step">
              <span class="step-badge">8 强席位</span>
              <div class="team-tags-list">
                <span v-for="team in quarterfinalists" :key="team.id" class="mini-team-tag secondary">
                  <img :src="'https://flagcdn.com/w40/' + team.code + '.png'" class="team-flag-img vertical-middle" :alt="team.name" />
                  <span class="vertical-middle">{{ team.name }}</span>
                </span>
              </div>
            </div>

            <div class="road-step">
              <span class="step-badge">最佳小组第三</span>
              <div class="team-tags-list">
                <span v-for="teamId in manualQualifiedThirds" :key="teamId" class="mini-team-tag info">
                  <img :src="'https://flagcdn.com/w40/' + getTeam(teamId).code + '.png'" class="team-flag-img vertical-middle" :alt="getTeam(teamId).name" />
                  <span class="vertical-middle">{{ getTeam(teamId).name }}</span>
                </span>
              </div>
            </div>

            <div class="report-share-actions">
              <button class="btn btn-primary" @click="downloadJSON">
                📥 下载数据 (JSON)
              </button>
              <button class="btn btn-primary" @click="exportReportImage" :disabled="isExporting">
                🖼️ {{ isExporting ? '正在生成图片...' : '导出预测图片' }}
              </button>
              <button class="btn btn-secondary" @click="currentPhase = 'knockout'">
                &larr; 返回对阵图修改
              </button>
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- Celebration Screen Confetti Canvas -->
    <canvas ref="confettiCanvas" class="confetti-canvas-overlay" v-show="showConfetti"></canvas>

    <!-- DeepSeek AI Simulation Summary Modal -->
    <transition name="fade-scale">
      <div v-if="showAIModal" class="ai-modal-overlay">
        <div class="ai-modal-card glass-panel animate-pop-in">
          <div class="modal-header">
            <span class="modal-icon">🤖</span>
            <h3>{{ aiSimulationResult?.knockoutWinners && Object.keys(aiSimulationResult.knockoutWinners).length > 0 ? 'DeepSeek AI 全局预测模拟总结' : 'DeepSeek AI ' + (aiSimulationResult?.groupMatches?.[0]?.groupId || '') + '组预测模拟总结' }}</h3>
            <button class="btn-close-modal" @click="showAIModal = false">×</button>
          </div>
          <div class="modal-body scrollable-content">
            <div class="summary-markdown-view" v-html="aiSummaryHtml"></div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-secondary" @click="showAIModal = false">
              取消
            </button>
            <button class="btn btn-primary btn-confirm-simulate" @click="applyAISimulation">
              <span class="btn-glow-orb"></span>
              确认并落实预测结果
            </button>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch, nextTick } from 'vue';

// Define core phases
const phases = [
  { id: 'group', name: '小组赛预测', icon: '⚽' },
  { id: 'third', name: '小组第三筛选', icon: '📊' },
  { id: 'knockout', name: '淘汰赛对阵', icon: '🏆' },
  { id: 'report', name: '预测报告', icon: '📄' }
];

const currentPhase = ref('group');
const hoveredTeamId = ref(null);
const showConfetti = ref(false);
const confettiCanvas = ref(null);
const isSimulating = ref(false);
const teamProbabilities = ref({});

const isGlobalSimulating = ref(false);
const simulatingGroupId = ref(null);

const showAIModal = ref(false);
const aiSimulationResult = ref(null);
const aiSummaryHtml = computed(() => {
  return renderMarkdown(aiSimulationResult.value?.summary);
});

const renderMarkdown = (md) => {
  if (!md) return '';
  let html = md;
  // Escape HTML entities to prevent XSS
  html = html
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  
  // Headers
  html = html.replace(/^# (.*?)$/gm, '<h1>$1</h1>');
  html = html.replace(/^## (.*?)$/gm, '<h2>$1</h2>');
  html = html.replace(/^### (.*?)$/gm, '<h3>$1</h3>');
  
  // Bold
  html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
  
  // Bullet lists
  html = html.replace(/^\* (.*?)$/gm, '<li>$1</li>');
  html = html.replace(/^(<li>.*?<\/li>)/gms, '<ul>$1</ul>');
  
  // Line breaks
  html = html.replace(/\n/g, '<br/>');
  
  return html;
};

// Database of 48 teams (updated with ISO codes for FlagCDN icons)
const teamsDb = ref({
  // Group A
  'MEX': { id: 'MEX', name: '墨西哥', flag: '🇲🇽', code: 'mx', strength: 81, isHost: true },
  'RSA': { id: 'RSA', name: '南非', flag: '🇿🇦', code: 'za', strength: 71 },
  'KOR': { id: 'KOR', name: '韩国', flag: '🇰🇷', code: 'kr', strength: 79 },
  'CZE': { id: 'CZE', name: '捷克', flag: '🇨🇿', code: 'cz', strength: 78 },
  // Group B
  'CAN': { id: 'CAN', name: '加拿大', flag: '🇨🇦', code: 'ca', strength: 78, isHost: true },
  'BIH': { id: 'BIH', name: '波黑', flag: '🇧🇦', code: 'ba', strength: 74 },
  'QAT': { id: 'QAT', name: '卡塔尔', flag: '🇶🇦', code: 'qa', strength: 70 },
  'SUI': { id: 'SUI', name: '瑞士', flag: '🇨🇭', code: 'ch', strength: 83 },
  // Group C
  'BRA': { id: 'BRA', name: '巴西', flag: '🇧🇷', code: 'br', strength: 92 },
  'MAR': { id: 'MAR', name: '摩洛哥', flag: '🇲🇦', code: 'ma', strength: 88 },
  'HAI': { id: 'HAI', name: '海地', flag: '🇭🇹', code: 'ht', strength: 65 },
  'SCO': { id: 'SCO', name: '苏格兰', flag: '🏴󠁧󠁢󠁳󠁣󠁴󠁿', code: 'gb-sct', strength: 76 },
  // Group D
  'USA': { id: 'USA', name: '美国', flag: '🇺🇸', code: 'us', strength: 84, isHost: true },
  'PAR': { id: 'PAR', name: '巴拉圭', flag: '🇵🇾', code: 'py', strength: 77 },
  'AUS': { id: 'AUS', name: '澳大利亚', flag: '🇦🇺', code: 'au', strength: 78 },
  'TUR': { id: 'TUR', name: '土耳其', flag: '🇹🇷', code: 'tr', strength: 81 },
  // Group E
  'GER': { id: 'GER', name: '德国', flag: '🇩🇪', code: 'de', strength: 89 },
  'CUW': { id: 'CUW', name: '库拉索', flag: '🇨🇼', code: 'cw', strength: 62 },
  'CIV': { id: 'CIV', name: '科特迪瓦', flag: '🇨🇮', code: 'ci', strength: 80 },
  'ECU': { id: 'ECU', name: '厄瓜多尔', flag: '🇪🇨', code: 'ec', strength: 82 },
  // Group F
  'NED': { id: 'NED', name: '荷兰', flag: '🇳🇱', code: 'nl', strength: 88 },
  'JPN': { id: 'JPN', name: '日本', flag: '🇯🇵', code: 'jp', strength: 85 },
  'SWE': { id: 'SWE', name: '瑞典', flag: '🇸🇪', code: 'se', strength: 81 },
  'TUN': { id: 'TUN', name: '突尼斯', flag: '🇹🇳', code: 'tn', strength: 75 },
  // Group G
  'BEL': { id: 'BEL', name: '比利时', flag: '🇧🇪', code: 'be', strength: 86 },
  'EGY': { id: 'EGY', name: '埃及', flag: '🇪🇬', code: 'eg', strength: 80 },
  'IRN': { id: 'IRN', name: '伊朗', flag: '🇮🇷', code: 'ir', strength: 77 },
  'NZL': { id: 'NZL', name: '新西兰', flag: '🇳🇿', code: 'nz', strength: 68 },
  // Group H
  'ESP': { id: 'ESP', name: '西班牙', flag: '🇪🇸', code: 'es', strength: 91 },
  'CPV': { id: 'CPV', name: '佛得角', flag: '🇨🇻', code: 'cv', strength: 71 },
  'KSA': { id: 'KSA', name: '沙特阿拉伯', flag: '🇸🇦', code: 'sa', strength: 72 },
  'URU': { id: 'URU', name: '乌拉圭', flag: '🇺🇾', code: 'uy', strength: 86 },
  // Group I
  'FRA': { id: 'FRA', name: '法国', flag: '🇫🇷', code: 'fr', strength: 93 },
  'SEN': { id: 'SEN', name: '塞内加尔', flag: '🇸🇳', code: 'sn', strength: 82 },
  'IRQ': { id: 'IRQ', name: '伊拉克', flag: '🇮🇶', code: 'iq', strength: 71 },
  'NOR': { id: 'NOR', name: '挪威', flag: '🇳🇴', code: 'no', strength: 80 },
  // Group J
  'ARG': { id: 'ARG', name: '阿根廷', flag: '🇦🇷', code: 'ar', strength: 94, isChampion: true },
  'ALG': { id: 'ALG', name: '阿尔及利亚', flag: '🇩🇿', code: 'dz', strength: 79 },
  'AUT': { id: 'AUT', name: '奥地利', flag: '🇦🇹', code: 'at', strength: 81 },
  'JOR': { id: 'JOR', name: '约旦', flag: '🇯🇴', code: 'jo', strength: 68 },
  // Group K
  'POR': { id: 'POR', name: '葡萄牙', flag: '🇵🇹', code: 'pt', strength: 90 },
  'COD': { id: 'COD', name: '刚果（金）', flag: '🇨🇩', code: 'cd', strength: 73 },
  'UZB': { id: 'UZB', name: '乌兹别克斯坦', flag: '🇺🇿', code: 'uz', strength: 74 },
  'COL': { id: 'COL', name: '哥伦比亚', flag: '🇨🇴', code: 'co', strength: 85 },
  // Group L
  'ENG': { id: 'ENG', name: '英格兰', flag: '🏴 injustice', code: 'gb-eng', strength: 92 },
  'CRO': { id: 'CRO', name: '克罗地亚', flag: '🇭🇷', code: 'hr', strength: 84 },
  'GHA': { id: 'GHA', name: '加纳', flag: '🇬🇭', code: 'gh', strength: 76 },
  'PAN': { id: 'PAN', name: '巴拿马', flag: '🇵🇦', code: 'pa', strength: 71 }
});

const getTeam = (id) => teamsDb.value[id] || { id, name: id, flag: '❓', code: 'un', strength: 50 };

// World Cup Group configuration
const groups = ref([
  { id: 'A', name: 'A', showMatches: false, predictionMode: 'rank', teamIds: ['MEX', 'RSA', 'KOR', 'CZE'] },
  { id: 'B', name: 'B', showMatches: false, predictionMode: 'rank', teamIds: ['CAN', 'BIH', 'QAT', 'SUI'] },
  { id: 'C', name: 'C', showMatches: false, predictionMode: 'rank', teamIds: ['BRA', 'MAR', 'HAI', 'SCO'] },
  { id: 'D', name: 'D', showMatches: false, predictionMode: 'rank', teamIds: ['USA', 'PAR', 'AUS', 'TUR'] },
  { id: 'E', name: 'E', showMatches: false, predictionMode: 'rank', teamIds: ['GER', 'CUW', 'CIV', 'ECU'] },
  { id: 'F', name: 'F', showMatches: false, predictionMode: 'rank', teamIds: ['NED', 'JPN', 'SWE', 'TUN'] },
  { id: 'G', name: 'G', showMatches: false, predictionMode: 'rank', teamIds: ['BEL', 'EGY', 'IRN', 'NZL'] },
  { id: 'H', name: 'H', showMatches: false, predictionMode: 'rank', teamIds: ['ESP', 'CPV', 'KSA', 'URU'] },
  { id: 'I', name: 'I', showMatches: false, predictionMode: 'rank', teamIds: ['FRA', 'SEN', 'IRQ', 'NOR'] },
  { id: 'J', name: 'J', showMatches: false, predictionMode: 'rank', teamIds: ['ARG', 'ALG', 'AUT', 'JOR'] },
  { id: 'K', name: 'K', showMatches: false, predictionMode: 'rank', teamIds: ['POR', 'COD', 'UZB', 'COL'] },
  { id: 'L', name: 'L', showMatches: false, predictionMode: 'rank', teamIds: ['ENG', 'CRO', 'GHA', 'PAN'] }
]);

const scheduleTimes = {
  'A': ["06月12日 03:00", "06月12日 10:00", "06月19日 09:00", "06月19日 00:00", "06月25日 09:00", "06月25日 09:00"],
  'B': ["06月13日 03:00", "06月14日 03:00", "06月19日 09:00", "06月19日 03:00", "06月25日 03:00", "06月25日 03:00"],
  'C': ["06月14日 06:00", "06月14日 09:00", "06月20日 08:30", "06月20日 06:00", "06月25日 06:00", "06月25日 06:00"],
  'D': ["06月13日 09:00", "06月14日 12:00", "06月20日 03:00", "06月20日 11:00", "06月26日 10:00", "06月26日 10:00"],
  'E': ["06月15日 01:00", "06月15日 07:00", "06月21日 04:00", "06月21日 08:00", "06月26日 04:00", "06月26日 04:00"],
  'F': ["06月15日 04:00", "06月15日 10:00", "06月21日 01:00", "06月21日 12:00", "06月26日 07:00", "06月26日 07:00"],
  'G': ["06月16日 03:00", "06月16日 09:00", "06月22日 03:00", "06月22日 09:00", "06月27日 11:00", "06月27日 11:00"],
  'H': ["06月16日 00:00", "06月16日 06:00", "06月22日 00:00", "06月22日 06:00", "06月27日 08:00", "06月27日 08:00"],
  'I': ["06月17日 03:00", "06月17日 06:00", "06月23日 05:00", "06月23日 08:00", "06月27日 03:00", "06月27日 03:00"],
  'J': ["06月17日 09:00", "06月17日 12:00", "06月23日 01:00", "06月23日 11:00", "06月28日 10:00", "06月28日 10:00"],
  'K': ["06月18日 01:00", "06月18日 10:00", "06月24日 01:00", "06月24日 10:00", "06月28日 07:30", "06月28日 07:30"],
  'L': ["06月18日 04:00", "06月18日 07:00", "06月24日 04:00", "06月24日 07:00", "06月28日 05:00", "06月28日 05:00"]
};

// Dynamic Date Generator for matches
const getMatchDate = (groupId, matchIndex) => {
  return scheduleTimes[groupId]?.[matchIndex] || "未知时间";
};

// Group Match Generator
const generateMatches = (groupId, teamIds) => {
  const pairings = [
    { homeId: teamIds[0], awayId: teamIds[1] },
    { homeId: teamIds[2], awayId: teamIds[3] },
    { homeId: teamIds[0], awayId: teamIds[2] },
    { homeId: teamIds[1], awayId: teamIds[3] },
    { homeId: teamIds[0], awayId: teamIds[3] },
    { homeId: teamIds[1], awayId: teamIds[2] }
  ];

  return pairings.map((p, idx) => ({
    homeId: p.homeId,
    awayId: p.awayId,
    homeScore: null,
    awayScore: null,
    date: getMatchDate(groupId, idx)
  }));
};

// State: Store group standings and matches
const initializeGroupsData = () => {
  groups.value.forEach(g => {
    g.predictionMode = 'score';
    g.showMatches = false;

    // Current group standings (holds dynamic PTS, GD, GS, W, D, L etc.)
    g.standings = g.teamIds.map((id, index) => {
      const t = getTeam(id);
      return {
        id,
        name: t.name,
        flag: t.flag,
        code: t.code,
        strength: t.strength,
        isHost: t.isHost,
        isChampion: t.isChampion,
        gp: 0, w: 0, d: 0, l: 0, gd: 0, gs: 0, pts: 0
      };
    });

    g.matches = generateMatches(g.id, g.teamIds);
  });
};

const toggleGroupMatches = (groupId) => {
  const g = groups.value.find(x => x.id === groupId);
  if (g) {
    g.showMatches = !g.showMatches;
  }
};

// Calculate standings table when score input changes
const onScoreChanged = (groupId) => {
  const g = groups.value.find(x => x.id === groupId);
  if (!g) return;

  // Flag that this group is using Match Score Mode now
  g.predictionMode = 'score';

  // Reset standings parameters
  g.standings.forEach(team => {
    team.gp = 0;
    team.w = 0;
    team.d = 0;
    team.l = 0;
    team.gd = 0;
    team.gs = 0;
    team.pts = 0;
  });

  // Re-calculate
  g.matches.forEach(match => {
    if (match.homeScore !== null && match.awayScore !== null && match.homeScore !== '' && match.awayScore !== '') {
      const home = g.standings.find(t => t.id === match.homeId);
      const away = g.standings.find(t => t.id === match.awayId);

      if (home && away) {
        home.gp++;
        away.gp++;
        home.gs += match.homeScore;
        away.gs += match.awayScore;
        home.gd += (match.homeScore - match.awayScore);
        away.gd += (match.awayScore - match.homeScore);

        if (match.homeScore > match.awayScore) {
          home.w++;
          home.pts += 3;
          away.l++;
        } else if (match.homeScore < match.awayScore) {
          away.w++;
          away.pts += 3;
          home.l++;
        } else {
          home.d++;
          home.pts += 1;
          away.d++;
          away.pts += 1;
        }
      }
    }
  });

  calculateProbabilities();
  saveProgress();
};

// Simulate Match score based on Poisson-weighted team strengths
const simulateMatch = (strengthA, strengthB) => {
  const ratio = strengthA / strengthB;
  const pA = 0.25 * Math.sqrt(ratio);
  const pB = 0.25 / Math.sqrt(ratio);

  let homeScore = 0;
  let awayScore = 0;
  for (let i = 0; i < 5; i++) {
    if (Math.random() < pA) homeScore++;
    if (Math.random() < pB) awayScore++;
  }
  return { homeScore, awayScore };
};

// Simulate a single group
const simulateGroup = async (groupId) => {
  simulatingGroupId.value = groupId;
  isSimulating.value = true;
  
  try {
    // Construct payload
    const payload = {
      groupId: groupId,
      groups: groups.value.map(g => ({
        id: g.id,
        matches: g.matches.map(m => ({
          homeId: m.homeId,
          awayId: m.awayId,
          homeScore: (m.homeScore !== null && m.homeScore !== '') ? parseInt(m.homeScore) : null,
          awayScore: (m.awayScore !== null && m.awayScore !== '') ? parseInt(m.awayScore) : null
        }))
      }))
    };

    const response = await fetch('/api/worldcup/simulate', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      const errText = await response.text();
      throw new Error(errText || `HTTP error ${response.status}`);
    }

    const result = await response.json();
    aiSimulationResult.value = result;
    showAIModal.value = true;
  } catch (error) {
    console.error('Group AI Simulation failed:', error);
    alert('AI 模拟本组预测失败，请确认后端已启动，且 appsettings.json 中已配置 DeepSeek API Key！\n错误详情: ' + error.message);
  } finally {
    simulatingGroupId.value = null;
    isSimulating.value = false;
  }
};

// Simulate all groups at once
const autoSimulateAll = async () => {
  isGlobalSimulating.value = true;
  isSimulating.value = true;
  
  try {
    // Construct payload
    const payload = {
      groups: groups.value.map(g => ({
        id: g.id,
        matches: g.matches.map(m => ({
          homeId: m.homeId,
          awayId: m.awayId,
          homeScore: (m.homeScore !== null && m.homeScore !== '') ? parseInt(m.homeScore) : null,
          awayScore: (m.awayScore !== null && m.awayScore !== '') ? parseInt(m.awayScore) : null
        }))
      }))
    };

    const response = await fetch('/api/worldcup/simulate', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(payload)
    });

    if (!response.ok) {
      const errText = await response.text();
      throw new Error(errText || `HTTP error ${response.status}`);
    }

    const result = await response.json();
    aiSimulationResult.value = result;
    showAIModal.value = true;
  } catch (error) {
    console.error('AI Simulation failed:', error);
    alert('AI 模拟预测失败，请确认后端已启动，且 appsettings.json 中已配置 DeepSeek API Key！\n错误详情: ' + error.message);
  } finally {
    isGlobalSimulating.value = false;
    isSimulating.value = false;
  }
};

const applyAISimulation = () => {
  if (!aiSimulationResult.value) return;

  const result = aiSimulationResult.value;

  // 1. Apply group stage matches scores
  result.groupMatches.forEach(simMatch => {
    groups.value.forEach(g => {
      if (g.id === simMatch.groupId) {
        const m = g.matches.find(match => match.homeId === simMatch.homeId && match.awayId === simMatch.awayId);
        if (m) {
          m.homeScore = simMatch.homeScore;
          m.awayScore = simMatch.awayScore;
        }
      }
    });
  });

  // 2. Trigger recalculations for the affected groups
  const affectedGroupIds = new Set(result.groupMatches.map(m => m.groupId));
  affectedGroupIds.forEach(groupId => {
    const g = groups.value.find(x => x.id === groupId);
    if (!g) return;

    g.predictionMode = 'score';
    g.standings.forEach(team => {
      team.gp = 0;
      team.w = 0;
      team.d = 0;
      team.l = 0;
      team.gd = 0;
      team.gs = 0;
      team.pts = 0;
    });

    g.matches.forEach(match => {
      if (match.homeScore !== null && match.awayScore !== null && match.homeScore !== '' && match.awayScore !== '') {
        const home = g.standings.find(t => t.id === match.homeId);
        const away = g.standings.find(t => t.id === match.awayId);

        if (home && away) {
          home.gp++;
          away.gp++;
          home.gs += match.homeScore;
          away.gs += match.awayScore;
          home.gd += (match.homeScore - match.awayScore);
          away.gd += (match.awayScore - match.homeScore);

          if (match.homeScore > match.awayScore) {
            home.w++;
            home.pts += 3;
            away.l++;
          } else if (match.homeScore < match.awayScore) {
            away.w++;
            away.pts += 3;
            home.l++;
          } else {
            home.d++;
            home.pts += 1;
            away.d++;
            away.pts += 1;
          }
        }
      }
    });
  });

  const isFullSimulation = result.knockoutWinners && Object.keys(result.knockoutWinners).length > 0;

  if (isFullSimulation) {
    // 3. Set manualQualifiedThirds
    if (result.qualifiedThirds && result.qualifiedThirds.length === 8) {
      manualQualifiedThirds.value = [...result.qualifiedThirds];
    } else {
      const list = [];
      groups.value.forEach(g => {
        const sorted = getSortedStandings(g);
        const thirdPlaceTeam = sorted[2];
        if (thirdPlaceTeam) {
          list.push({ groupId: g.id, team: thirdPlaceTeam });
        }
      });
      list.sort((a, b) => {
        if (b.team.pts !== a.team.pts) return b.team.pts - a.team.pts;
        if (b.team.gd !== a.team.gd) return b.team.gd - a.team.gd;
        if (b.team.gs !== a.team.gs) return b.team.gs - a.team.gs;
        return b.team.strength - a.team.strength;
      });
      manualQualifiedThirds.value = list.slice(0, 8).map(x => x.team.id);
    }

    // 4. Set knockoutMatches Winners and propagate
    generateKnockout();

    // Apply round by round to propagate the tree correctly
    // Round of 32
    for (let i = 1; i <= 16; i++) {
      const mId = 'M' + i;
      const winnerId = result.knockoutWinners[mId];
      const match = knockoutMatches.value[mId];
      if (match && winnerId) {
        if (match.team1?.id === winnerId) {
          advanceTeam(mId, 'team1');
        } else if (match.team2?.id === winnerId) {
          advanceTeam(mId, 'team2');
        }
      }
    }

    // Round of 16
    for (let i = 17; i <= 24; i++) {
      const mId = 'M' + i;
      const winnerId = result.knockoutWinners[mId];
      const match = knockoutMatches.value[mId];
      if (match && winnerId) {
        if (match.team1?.id === winnerId) {
          advanceTeam(mId, 'team1');
        } else if (match.team2?.id === winnerId) {
          advanceTeam(mId, 'team2');
        }
      }
    }

    // Quarterfinals
    for (let i = 25; i <= 28; i++) {
      const mId = 'M' + i;
      const winnerId = result.knockoutWinners[mId];
      const match = knockoutMatches.value[mId];
      if (match && winnerId) {
        if (match.team1?.id === winnerId) {
          advanceTeam(mId, 'team1');
        } else if (match.team2?.id === winnerId) {
          advanceTeam(mId, 'team2');
        }
      }
    }

    // Semifinals
    ['M29', 'M30'].forEach(mId => {
      const winnerId = result.knockoutWinners[mId];
      const match = knockoutMatches.value[mId];
      if (match && winnerId) {
        if (match.team1?.id === winnerId) {
          advanceTeam(mId, 'team1');
        } else if (match.team2?.id === winnerId) {
          advanceTeam(mId, 'team2');
        }
      }
    });

    // Final & 3rd Place Match
    ['M32', 'M31'].forEach(mId => {
      const winnerId = result.knockoutWinners[mId];
      const match = knockoutMatches.value[mId];
      if (match && winnerId) {
        if (match.team1?.id === winnerId) {
          advanceTeam(mId, 'team1');
        } else if (match.team2?.id === winnerId) {
          advanceTeam(mId, 'team2');
        }
      }
    });
  }

  calculateProbabilities();
  saveProgress();

  showAIModal.value = false;
  aiSimulationResult.value = null;

  if (isFullSimulation) {
    // Navigate directly to the final report screen
    currentPhase.value = 'report';
  }
};

const getSortedStandings = (group) => {
  return [...group.standings].sort((a, b) => {
    if (b.pts !== a.pts) return b.pts - a.pts;
    if (b.gd !== a.gd) return b.gd - a.gd;
    if (b.gs !== a.gs) return b.gs - a.gs;
    return b.strength - a.strength;
  });
};

const getTeamRankInGroup = (group, teamId) => {
  const sorted = getSortedStandings(group);
  return sorted.findIndex(t => t.id === teamId) + 1;
};

const calculateProbabilities = () => {
  const runs = 500;
  const qualifiedCounts = {};

  // Initialize counts
  groups.value.forEach(g => {
    g.teamIds.forEach(id => {
      qualifiedCounts[id] = 0;
    });
  });

  for (let r = 0; r < runs; r++) {
    const simGroupStandings = {};

    groups.value.forEach(g => {
      const standingsDict = {};
      g.teamIds.forEach(id => {
        const team = g.standings.find(t => t.id === id);
        standingsDict[id] = {
          id,
          name: team.name,
          code: team.code,
          strength: team.strength,
          gp: 0, w: 0, d: 0, l: 0, gd: 0, gs: 0, pts: 0
        };
      });

      g.matches.forEach(match => {
        let homeScore = match.homeScore;
        let awayScore = match.awayScore;

        if (homeScore === null || homeScore === '' || awayScore === null || awayScore === '') {
          const homeTeam = getTeam(match.homeId);
          const awayTeam = getTeam(match.awayId);
          const result = simulateMatch(homeTeam.strength, awayTeam.strength);
          homeScore = result.homeScore;
          awayScore = result.awayScore;
        }

        const home = standingsDict[match.homeId];
        const away = standingsDict[match.awayId];

        home.gp++;
        away.gp++;
        home.gs += homeScore;
        away.gs += awayScore;
        home.gd += (homeScore - awayScore);
        away.gd += (awayScore - homeScore);

        if (homeScore > awayScore) {
          home.w++;
          home.pts += 3;
          away.l++;
        } else if (homeScore < awayScore) {
          away.w++;
          away.pts += 3;
          home.l++;
        } else {
          home.d++;
          home.pts += 1;
          away.d++;
          away.pts += 1;
        }
      });

      const standingsArray = Object.values(standingsDict);
      standingsArray.sort((a, b) => {
        if (b.pts !== a.pts) return b.pts - a.pts;
        if (b.gd !== a.gd) return b.gd - a.gd;
        if (b.gs !== a.gs) return b.gs - a.gs;
        return b.strength - a.strength;
      });

      simGroupStandings[g.id] = standingsArray;
    });

    const simQualifiedThirds = [];
    groups.value.forEach(g => {
      const standings = simGroupStandings[g.id];
      qualifiedCounts[standings[0].id]++;
      qualifiedCounts[standings[1].id]++;
      simQualifiedThirds.push({
        groupId: g.id,
        team: standings[2]
      });
    });

    simQualifiedThirds.sort((a, b) => {
      if (b.team.pts !== a.team.pts) return b.team.pts - a.team.pts;
      if (b.team.gd !== a.team.gd) return b.team.gd - a.team.gd;
      if (b.team.gs !== a.team.gs) return b.team.gs - a.team.gs;
      return b.team.strength - a.team.strength;
    });

    for (let i = 0; i < 8; i++) {
      if (simQualifiedThirds[i]) {
        qualifiedCounts[simQualifiedThirds[i].team.id]++;
      }
    }
  }

  const probs = {};
  groups.value.forEach(g => {
    g.teamIds.forEach(id => {
      const count = qualifiedCounts[id] || 0;
      probs[id] = Math.round((count / runs) * 100);
    });
  });

  teamProbabilities.value = probs;
};

// ==========================================
// PHASE 2: THIRD PLACED SCREEN STATE
// ==========================================
const thirdPlaceList = ref([]);
const manualQualifiedThirds = ref([]); // Holds array of team IDs

const proceedToThirdPlace = () => {
  // Collect 3rd placed team from each of 12 groups
  const list = [];
  groups.value.forEach(g => {
    const sorted = getSortedStandings(g);
    const thirdPlaceTeam = sorted[2]; // Index 2 is the 3rd placed team
    if (thirdPlaceTeam) {
      list.push({
        groupId: g.id,
        team: thirdPlaceTeam
      });
    }
  });

  // Sort them: PTS desc, GD desc, GS desc
  list.sort((a, b) => {
    if (b.team.pts !== a.team.pts) return b.team.pts - a.team.pts;
    if (b.team.gd !== a.team.gd) return b.team.gd - a.team.gd;
    if (b.team.gs !== a.team.gs) return b.team.gs - a.team.gs;
    return b.team.strength - a.team.strength;
  });

  thirdPlaceList.value = list;

  // Auto populate the top 8 if not already modified manually by user
  if (manualQualifiedThirds.value.length !== 8) {
    autoSelectBestThirds();
  }

  currentPhase.value = 'third';
  saveProgress();
};

const autoSelectBestThirds = () => {
  // Pick top 8 sorted third-placed teams
  manualQualifiedThirds.value = thirdPlaceList.value.slice(0, 8).map(x => x.team.id);
  saveProgress();
};

const isThirdQualified = (teamId) => {
  return manualQualifiedThirds.value.includes(teamId);
};

const toggleManualThirdQualify = (teamId) => {
  if (manualQualifiedThirds.value.includes(teamId)) {
    // Remove
    manualQualifiedThirds.value = manualQualifiedThirds.value.filter(id => id !== teamId);
  } else {
    // Add (limit to 8)
    if (manualQualifiedThirds.value.length < 8) {
      manualQualifiedThirds.value.push(teamId);
    } else {
      alert('您最多只能选择 8 个出线的小组第三名！请先取消勾选其他队伍。');
    }
  }
  saveProgress();
};

// ==========================================
// PHASE 3: KNOCKOUT STAGE STATE & TREE
// ==========================================
// Matches are stored as a flat array with dynamic round promotion
const knockoutMatches = ref({});

// Generate 32-Round pairings and build initial tree (Updated pairing rules from Bilibili video screenshot)
const generateKnockout = () => {
  currentPhase.value = 'knockout';

  // Gather 12 winners and 12 runners-up
  const winners = {};
  const runners = {};
  groups.value.forEach(g => {
    const sorted = getSortedStandings(g);
    winners[g.id] = sorted[0];
    runners[g.id] = sorted[1];
  });

  // Gather 8 third-place teams (ranked 1st to 8th in their qualified list)
  const sortedThirdIds = manualQualifiedThirds.value;
  const thirds = sortedThirdIds.map(id => {
    let found = null;
    groups.value.forEach(g => {
      const match = g.standings.find(t => t.id === id);
      if (match) found = match;
    });
    return found;
  });

  // Pairing layout matched exactly with the Bilibili screenshot tree layout:
  // LEFT SIDE (top to bottom):
  // - Match 1 (M1): A1 vs 3rd (Pick 1)
  // - Match 2 (M2): L1 vs 3rd (Pick 2)
  // - Match 3 (M3): C1 vs F2
  // - Match 4 (M4): E2 vs I2
  // - Match 5 (M5): B1 vs 3rd (Pick 3)
  // - Match 6 (M6): K1 vs 3rd (Pick 4)
  // - Match 7 (M7): J1 vs H2
  // - Match 8 (M8): D2 vs G2
  //
  // RIGHT SIDE (top to bottom):
  // - Match 9 (M9): E1 vs 3rd (Pick 5)
  // - Match 10 (M10): I1 vs 3rd (Pick 6)
  // - Match 11 (M11): F1 vs C2
  // - Match 12 (M12): A2 vs B2
  // - Match 13 (M13): D1 vs 3rd (Pick 7)
  // - Match 14 (M14): G1 vs 3rd (Pick 8)
  // - Match 15 (M15): H1 vs J2
  // - Match 16 (M16): K2 vs L2

  const initialMatches = {
    // LEFT HALF (Matches 1-8)
    'M1': { id: 'M1', parentId: 'M17', slot: 'team1', team1: winners['A'], team2: thirds[0], team1Seed: 'A1', team2Seed: '3rd-1', winner: null },
    'M2': { id: 'M2', parentId: 'M17', slot: 'team2', team1: winners['L'], team2: thirds[1], team1Seed: 'L1', team2Seed: '3rd-2', winner: null },
    
    'M3': { id: 'M3', parentId: 'M18', slot: 'team1', team1: winners['C'], team2: runners['F'], team1Seed: 'C1', team2Seed: 'F2', winner: null },
    'M4': { id: 'M4', parentId: 'M18', slot: 'team2', team1: runners['E'], team2: runners['I'], team1Seed: 'E2', team2Seed: 'I2', winner: null },
    
    'M5': { id: 'M5', parentId: 'M19', slot: 'team1', team1: winners['B'], team2: thirds[2], team1Seed: 'B1', team2Seed: '3rd-3', winner: null },
    'M6': { id: 'M6', parentId: 'M19', slot: 'team2', team1: winners['K'], team2: thirds[3], team1Seed: 'K1', team2Seed: '3rd-4', winner: null },
    
    'M7': { id: 'M7', parentId: 'M20', slot: 'team1', team1: winners['J'], team2: runners['H'], team1Seed: 'J1', team2Seed: 'H2', winner: null },
    'M8': { id: 'M8', parentId: 'M20', slot: 'team2', team1: runners['D'], team2: runners['G'], team1Seed: 'D2', team2Seed: 'G2', winner: null },

    // RIGHT HALF (Matches 9-16)
    'M9': { id: 'M9', parentId: 'M21', slot: 'team1', team1: winners['E'], team2: thirds[4], team1Seed: 'E1', team2Seed: '3rd-5', winner: null },
    'M10': { id: 'M10', parentId: 'M21', slot: 'team2', team1: winners['I'], team2: thirds[5], team1Seed: 'I1', team2Seed: '3rd-6', winner: null },
    
    'M11': { id: 'M11', parentId: 'M22', slot: 'team1', team1: winners['F'], team2: runners['C'], team1Seed: 'F1', team2Seed: 'C2', winner: null },
    'M12': { id: 'M12', parentId: 'M22', slot: 'team2', team1: runners['A'], team2: runners['B'], team1Seed: 'A2', team2Seed: 'B2', winner: null },
    
    'M13': { id: 'M13', parentId: 'M23', slot: 'team1', team1: winners['D'], team2: thirds[6], team1Seed: 'D1', team2Seed: '3rd-7', winner: null },
    'M14': { id: 'M14', parentId: 'M23', slot: 'team2', team1: winners['G'], team2: thirds[7], team1Seed: 'G1', team2Seed: '3rd-8', winner: null },
    
    'M15': { id: 'M15', parentId: 'M24', slot: 'team1', team1: winners['H'], team2: runners['J'], team1Seed: 'H1', team2Seed: 'J2', winner: null },
    'M16': { id: 'M16', parentId: 'M24', slot: 'team2', team1: runners['K'], team2: runners['L'], team1Seed: 'K2', team2Seed: 'L2', winner: null },

    // Round of 16 (Matches 17-24)
    'M17': { id: 'M17', parentId: 'M25', slot: 'team1', team1: null, team2: null, winner: null },
    'M18': { id: 'M18', parentId: 'M25', slot: 'team2', team1: null, team2: null, winner: null },
    'M19': { id: 'M19', parentId: 'M26', slot: 'team1', team1: null, team2: null, winner: null },
    'M20': { id: 'M20', parentId: 'M26', slot: 'team2', team1: null, team2: null, winner: null },
    'M21': { id: 'M21', parentId: 'M27', slot: 'team1', team1: null, team2: null, winner: null },
    'M22': { id: 'M22', parentId: 'M27', slot: 'team2', team1: null, team2: null, winner: null },
    'M23': { id: 'M23', parentId: 'M28', slot: 'team1', team1: null, team2: null, winner: null },
    'M24': { id: 'M24', parentId: 'M28', slot: 'team2', team1: null, team2: null, winner: null },

    // Quarterfinals (Matches 25-28)
    'M25': { id: 'M25', parentId: 'M29', slot: 'team1', team1: null, team2: null, winner: null },
    'M26': { id: 'M26', parentId: 'M29', slot: 'team2', team1: null, team2: null, winner: null },
    'M27': { id: 'M27', parentId: 'M30', slot: 'team1', team1: null, team2: null, winner: null },
    'M28': { id: 'M28', parentId: 'M30', slot: 'team2', team1: null, team2: null, winner: null },

    // Semifinals (Matches 29-30)
    'M29': { id: 'M29', parentId: 'M31', slot: 'team1', team1: null, team2: null, winner: null },
    'M30': { id: 'M30', parentId: 'M31', slot: 'team2', team1: null, team2: null, winner: null },

    // Grand Final (Match 31)
    'M31': { id: 'M31', parentId: null, slot: null, team1: null, team2: null, winner: null },

    // 3rd Place Match (Match 32)
    'M32': { id: 'M32', parentId: null, slot: null, team1: null, team2: null, winner: null }
  };

  // Preserve previous selections if teams matched
  if (Object.keys(knockoutMatches.value).length > 0) {
    Object.keys(initialMatches).forEach(key => {
      const old = knockoutMatches.value[key];
      const cur = initialMatches[key];
      if (old && cur) {
        if (parseInt(key.substring(1)) <= 16) {
          cur.winner = old.winner;
        }
      }
    });
  }

  knockoutMatches.value = initialMatches;
  recalculatePromotions();
};

// Logic to advance a team to the next round slot
const advanceTeam = (matchId, slotChoice) => {
  const match = knockoutMatches.value[matchId];
  if (!match) return;

  const winningTeam = slotChoice === 'team1' ? match.team1 : match.team2;
  const losingTeam = slotChoice === 'team1' ? match.team2 : match.team1;
  if (!winningTeam) return; // Cannot advance a null team

  match.winner = slotChoice;

  // If this was a semifinal (M29 or M30), the loser goes to the 3rd place match (M32)
  if (matchId === 'M29') {
    knockoutMatches.value['M32'].team1 = losingTeam;
    if (knockoutMatches.value['M32'].winner && knockoutMatches.value['M32'].team1?.id !== losingTeam?.id) {
      knockoutMatches.value['M32'].winner = null;
    }
  } else if (matchId === 'M30') {
    knockoutMatches.value['M32'].team2 = losingTeam;
    if (knockoutMatches.value['M32'].winner && knockoutMatches.value['M32'].team2?.id !== losingTeam?.id) {
      knockoutMatches.value['M32'].winner = null;
    }
  }

  // Push to parent match
  if (match.parentId) {
    const parent = knockoutMatches.value[match.parentId];
    if (parent) {
      const oldVal = parent[match.slot];
      parent[match.slot] = winningTeam;

      // Reset subsequent tree branches if winner changed
      if (oldVal && oldVal.id !== winningTeam.id) {
        resetSubsequentTree(match.parentId);
      }
    }
  }

  // Check if champion was crowned
  if (matchId === 'M31') {
    triggerCelebration();
  }

  recalculatePromotions();
  saveProgress();
};

// Reset selections on forward nodes if bracket changes
const resetSubsequentTree = (matchId) => {
  const match = knockoutMatches.value[matchId];
  if (!match) return;

  match.winner = null;
  
  if (matchId === 'M29' || matchId === 'M30') {
    const m32 = knockoutMatches.value['M32'];
    m32.winner = null;
    if (matchId === 'M29') m32.team1 = null;
    if (matchId === 'M30') m32.team2 = null;
  }

  if (match.parentId) {
    const parent = knockoutMatches.value[match.parentId];
    if (parent) {
      parent[match.slot] = null;
      resetSubsequentTree(match.parentId);
    }
  }
};

// Re-run tree populating to ensure consistency
const recalculatePromotions = () => {
  for (let mNum = 1; mNum <= 31; mNum++) {
    const mId = 'M' + mNum;
    const match = knockoutMatches.value[mId];
    if (!match) continue;

    const winningTeam = match.winner === 'team1' ? match.team1 : (match.winner === 'team2' ? match.team2 : null);

    if (mId === 'M29' || mId === 'M30') {
      const losingTeam = match.winner === 'team1' ? match.team2 : (match.winner === 'team2' ? match.team1 : null);
      const m32 = knockoutMatches.value['M32'];
      if (m32) {
        if (mId === 'M29') m32.team1 = losingTeam;
        if (mId === 'M30') m32.team2 = losingTeam;
      }
    }

    if (winningTeam && match.parentId) {
      const parent = knockoutMatches.value[match.parentId];
      if (parent) {
        parent[match.slot] = winningTeam;
      }
    }
  }
};

// Run automated AI matches simulation for knockout brackets
const autoSimulateKnockout = () => {
  isSimulating.value = true;
  const rounds = [
    Array.from({ length: 16 }, (_, i) => 'M' + (i + 1)),
    Array.from({ length: 8 }, (_, i) => 'M' + (i + 17)),
    Array.from({ length: 4 }, (_, i) => 'M' + (i + 25)),
    ['M29', 'M30'],
    ['M32', 'M31']
  ];

  rounds.forEach(roundMatches => {
    roundMatches.forEach(mId => {
      const match = knockoutMatches.value[mId];
      if (!match) return;

      if (match.team1 && match.team2 && !match.winner) {
        const homeStrength = match.team1.strength;
        const awayStrength = match.team2.strength;

        const ratio = homeStrength / awayStrength;
        const probTeam1 = 0.5 * ratio;
        const roll = Math.random();

        if (roll < probTeam1) {
          advanceTeam(mId, 'team1');
        } else {
          advanceTeam(mId, 'team2');
        }
      }
    });
  });
  
  isSimulating.value = false;
};

// Grouped Left/Right Bracket computed lists for UI column rendering
const leftR32 = computed(() => Array.from({ length: 8 }, (_, i) => knockoutMatches.value['M' + (i + 1)] || {}));
const leftR16 = computed(() => Array.from({ length: 4 }, (_, i) => knockoutMatches.value['M' + (i + 17)] || {}));
const leftQf = computed(() => Array.from({ length: 2 }, (_, i) => knockoutMatches.value['M' + (i + 25)] || {}));
const leftSf = computed(() => [knockoutMatches.value['M29'] || {}]);

const rightSf = computed(() => [knockoutMatches.value['M30'] || {}]);
const rightQf = computed(() => Array.from({ length: 2 }, (_, i) => knockoutMatches.value['M' + (i + 27)] || {}));
const rightR16 = computed(() => Array.from({ length: 4 }, (_, i) => knockoutMatches.value['M' + (i + 21)] || {}));
const rightR32 = computed(() => Array.from({ length: 8 }, (_, i) => knockoutMatches.value['M' + (i + 9)] || {}));

const finalMatch = computed(() => knockoutMatches.value['M31']);
const thirdPlaceMatch = computed(() => knockoutMatches.value['M32']);

// Winners & report computed properties
const championTeam = computed(() => {
  const final = finalMatch.value;
  if (final && final.winner) {
    return final.winner === 'team1' ? final.team1 : final.team2;
  }
  return null;
});

const runnerUpTeam = computed(() => {
  const final = finalMatch.value;
  if (final && final.winner) {
    return final.winner === 'team1' ? final.team2 : final.team1;
  }
  return null;
});

const thirdPlaceWinnerTeam = computed(() => {
  const match32 = thirdPlaceMatch.value;
  if (match32 && match32.winner) {
    return match32.winner === 'team1' ? match32.team1 : match32.team2;
  }
  return null;
});

const semifinalists = computed(() => {
  const sfLeft = leftSf.value[0];
  const sfRight = rightSf.value[0];
  const list = [];
  if (sfLeft?.team1) list.push(sfLeft.team1);
  if (sfLeft?.team2) list.push(sfLeft.team2);
  if (sfRight?.team1) list.push(sfRight.team1);
  if (sfRight?.team2) list.push(sfRight.team2);
  return list;
});

const quarterfinalists = computed(() => {
  const list = [];
  for (let i = 25; i <= 28; i++) {
    const qf = knockoutMatches.value['M' + i];
    if (qf?.team1) list.push(qf.team1);
    if (qf?.team2) list.push(qf.team2);
  }
  return list;
});

// Celebration effects (confetti)
const triggerCelebration = () => {
  showConfetti.value = true;
  nextTick(() => {
    if (!confettiCanvas.value) return;
    const ctx = confettiCanvas.value.getContext('2d');
    const width = confettiCanvas.value.width = window.innerWidth;
    const height = confettiCanvas.value.height = window.innerHeight;

    const particles = Array.from({ length: 150 }, () => ({
      x: Math.random() * width,
      y: Math.random() * height - height,
      r: Math.random() * 6 + 4,
      d: Math.random() * height,
      color: `hsl(${Math.random() * 360}, 100%, 60%)`,
      tilt: Math.random() * 10 - 5,
      tiltAngleIncremental: Math.random() * 0.07 + 0.02,
      tiltAngle: 0
    }));

    let animationFrameId;
    const draw = () => {
      ctx.clearRect(0, 0, width, height);

      particles.forEach((p, idx) => {
        p.tiltAngle += p.tiltAngleIncremental;
        p.y += (Math.cos(p.d) + 3 + p.r / 2) / 2;
        p.x += Math.sin(p.tiltAngle);
        p.tilt = Math.sin(p.tiltAngle - idx / 3) * 15;

        ctx.beginPath();
        ctx.lineWidth = p.r;
        ctx.strokeStyle = p.color;
        ctx.moveTo(p.x + p.tilt + p.r / 2, p.y);
        ctx.lineTo(p.x + p.tilt, p.y + p.tilt + p.r / 2);
        ctx.stroke();

        if (p.y > height) {
          particles[idx] = {
            x: Math.random() * width,
            y: -20,
            r: p.r,
            d: p.d,
            color: p.color,
            tilt: Math.random() * 10 - 5,
            tiltAngleIncremental: p.tiltAngleIncremental,
            tiltAngle: 0
          };
        }
      });

      if (showConfetti.value) {
        animationFrameId = requestAnimationFrame(draw);
      }
    };

    draw();

    setTimeout(() => {
      showConfetti.value = false;
      cancelAnimationFrame(animationFrameId);
    }, 6000);
  });
};

// ==========================================
// ACCESS / VALIDATION LOGIC
// ==========================================
const isPhaseCompleted = (phaseId) => {
  if (phaseId === 'group') return true;
  if (phaseId === 'third') return manualQualifiedThirds.value.length === 8;
  if (phaseId === 'knockout') return championTeam.value !== null;
  return false;
};

const isPhaseAccessible = (phaseId) => {
  if (phaseId === 'group') return true;
  if (phaseId === 'third') return true;
  if (phaseId === 'knockout') return manualQualifiedThirds.value.length === 8;
  if (phaseId === 'report') return championTeam.value !== null;
  return false;
};

const switchPhase = (phaseId) => {
  if (isPhaseAccessible(phaseId)) {
    currentPhase.value = phaseId;
    if (phaseId === 'knockout' && Object.keys(knockoutMatches.value).length === 0) {
      generateKnockout();
    }
  }
};

const resetAll = () => {
  if (confirm('确认要重置所有预测比分和排名吗？您的进度将会被清空。')) {
    localStorage.removeItem('world_cup_predictor_state');
    initializeGroupsData();
    thirdPlaceList.value = [];
    manualQualifiedThirds.value = [];
    knockoutMatches.value = {};
    currentPhase.value = 'group';
    showConfetti.value = false;
    saveProgress(); // Ensure local storage reflects the reset state
  }
};

const isExporting = ref(false);

const exportReportImage = async () => {
  if (isExporting.value) return;
  isExporting.value = true;

  try {
    const champ = championTeam.value;
    const runner = runnerUpTeam.value;
    const thirdWinner = thirdPlaceWinnerTeam.value;
    const semis = semifinalists.value;
    const qfs = quarterfinalists.value;
    const thirds = manualQualifiedThirds.value.map(id => getTeam(id));

    // Gather unique flag codes
    const uniqueCodes = new Set();
    if (champ) uniqueCodes.add(champ.code);
    if (runner) uniqueCodes.add(runner.code);
    if (thirdWinner) uniqueCodes.add(thirdWinner.code);
    semis.forEach(t => { if (t) uniqueCodes.add(t.code); });
    qfs.forEach(t => { if (t) uniqueCodes.add(t.code); });
    thirds.forEach(t => { if (t) uniqueCodes.add(t.code); });

    const flagImages = {};
    const safeLoadImage = (url) => {
      return new Promise((resolve) => {
        const img = new Image();
        img.crossOrigin = 'anonymous';
        img.onload = () => resolve(img);
        img.onerror = () => {
          console.warn('Failed to load image:', url);
          resolve(null);
        };
        img.src = url;
      });
    };

    const loadPromises = Array.from(uniqueCodes).map(async (code) => {
      const img = await safeLoadImage(`https://flagcdn.com/w80/${code}.png`);
      if (img) {
        flagImages[code] = img;
      }
    });

    let bigChampImg = null;
    if (champ) {
      loadPromises.push((async () => {
        bigChampImg = await safeLoadImage(`https://flagcdn.com/w160/${champ.code}.png`);
      })());
    }

    await Promise.all(loadPromises);

    const canvas = document.createElement('canvas');
    canvas.width = 800;
    canvas.height = 1020;
    const ctx = canvas.getContext('2d');

    const drawRoundedRect = (c, x, y, w, h, r, fill, stroke, strokeWidth) => {
      c.beginPath();
      if (c.roundRect) {
        c.roundRect(x, y, w, h, r);
      } else {
        c.moveTo(x + r, y);
        c.lineTo(x + w - r, y);
        c.quadraticCurveTo(x + w, y, x + w, y + r);
        c.lineTo(x + w, y + h - r);
        c.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        c.lineTo(x + r, y + h);
        c.quadraticCurveTo(x, y + h, x, y + h - r);
        c.lineTo(x, y + r);
        c.quadraticCurveTo(x, y, x + r, y);
      }
      c.closePath();
      if (fill) {
        c.fillStyle = fill;
        c.fill();
      }
      if (stroke) {
        c.strokeStyle = stroke;
        c.lineWidth = strokeWidth || 1;
        c.stroke();
      }
    };

    // Draw background gradient
    const bgGrad = ctx.createLinearGradient(0, 0, 800, 1020);
    bgGrad.addColorStop(0, '#070a13');
    bgGrad.addColorStop(0.5, '#0f1524');
    bgGrad.addColorStop(1, '#1b1437');
    ctx.fillStyle = bgGrad;
    ctx.fillRect(0, 0, 800, 1020);

    // Faint decorative glow behind the champion card
    const radialGlow = ctx.createRadialGradient(400, 305, 50, 400, 305, 350);
    radialGlow.addColorStop(0, 'rgba(139, 92, 246, 0.15)');
    radialGlow.addColorStop(1, 'rgba(139, 92, 246, 0)');
    ctx.fillStyle = radialGlow;
    ctx.beginPath();
    ctx.arc(400, 305, 350, 0, Math.PI * 2);
    ctx.fill();

    // Header title
    ctx.textAlign = 'center';
    ctx.fillStyle = '#fbbf24'; // Gold
    ctx.font = 'bold 30px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('2026 美加墨世界杯预测报告', 400, 65);

    ctx.fillStyle = '#94a3b8'; // Muted secondary
    ctx.font = '14px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('智能预测平台 · 您的专属世界杯模拟预测图', 400, 100);

    // Subtle divider
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(100, 125);
    ctx.lineTo(700, 125);
    ctx.stroke();

    // Champion Card
    drawRoundedRect(ctx, 150, 150, 500, 290, 12, 'rgba(15, 21, 36, 0.65)', 'rgba(251, 191, 36, 0.35)', 2);

    ctx.fillStyle = '#f59e0b';
    ctx.font = '16px "Microsoft YaHei", sans-serif';
    ctx.fillText('⭐⭐⭐⭐⭐', 400, 185);

    ctx.fillStyle = '#fbbf24';
    ctx.font = 'bold 19px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('👑 世界杯总冠军 👑', 400, 215);

    // Champion flag
    const bigFlagW = 120;
    const bigFlagH = 78;
    const bigFlagX = 400 - bigFlagW / 2;
    const bigFlagY = 235;
    if (bigChampImg) {
      ctx.drawImage(bigChampImg, bigFlagX, bigFlagY, bigFlagW, bigFlagH);
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
      ctx.lineWidth = 2;
      ctx.strokeRect(bigFlagX, bigFlagY, bigFlagW, bigFlagH);
    } else {
      ctx.font = '48px sans-serif';
      ctx.fillText(champ?.flag || '🏳️', 400, 290);
    }

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 28px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText(champ ? champ.name : '未知冠军', 400, 355);

    ctx.fillStyle = '#94a3b8';
    ctx.font = '13px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('预测在 2026 年 7 月美加墨夏夜捧起金杯', 400, 385);

    // Runner-Up & Third Place
    // Runner-Up Card
    drawRoundedRect(ctx, 150, 460, 240, 60, 8, 'rgba(15, 21, 36, 0.5)', 'rgba(255, 255, 255, 0.08)', 1);
    ctx.textAlign = 'left';
    ctx.fillStyle = '#94a3b8';
    ctx.font = '13px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('亚军：', 170, 496);
    if (runner) {
      const rFlag = flagImages[runner.code];
      if (rFlag) {
        ctx.drawImage(rFlag, 215, 480, 28, 18);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.strokeRect(215, 480, 28, 18);
      }
      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 15px "Microsoft YaHei", "Segoe UI", sans-serif';
      ctx.fillText(runner.name, rFlag ? 252 : 215, 496);
    } else {
      ctx.fillStyle = '#64748b';
      ctx.fillText('待定', 215, 496);
    }

    // Third Place Card
    drawRoundedRect(ctx, 410, 460, 240, 60, 8, 'rgba(15, 21, 36, 0.5)', 'rgba(255, 255, 255, 0.08)', 1);
    ctx.textAlign = 'left';
    ctx.fillStyle = '#94a3b8';
    ctx.font = '13px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('季军：', 430, 496);
    if (thirdWinner) {
      const tFlag = flagImages[thirdWinner.code];
      if (tFlag) {
        ctx.drawImage(tFlag, 475, 480, 28, 18);
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.strokeRect(475, 480, 28, 18);
      }
      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 15px "Microsoft YaHei", "Segoe UI", sans-serif';
      ctx.fillText(thirdWinner.name, tFlag ? 512 : 475, 496);
    } else {
      ctx.fillStyle = '#64748b';
      ctx.fillText('待定', 475, 496);
    }

    const drawSectionTitle = (text, color, bgFill, borderStroke, x, y, width) => {
      drawRoundedRect(ctx, x, y, width, 24, 4, bgFill, borderStroke, 1);
      ctx.textAlign = 'center';
      ctx.fillStyle = color;
      ctx.font = 'bold 11px "Microsoft YaHei", sans-serif';
      ctx.fillText(text, x + width / 2, y + 16);
    };

    // Semifinalists (4强席位)
    drawSectionTitle('4 强 席 位', '#c084fc', 'rgba(139, 92, 246, 0.15)', 'rgba(139, 92, 246, 0.35)', 100, 545, 90);

    const cardW = 135;
    const cardH = 42;
    const cardGap = 20;

    semis.forEach((team, idx) => {
      if (idx >= 4) return;
      const x = 100 + idx * (cardW + cardGap);
      const y = 580;

      drawRoundedRect(ctx, x, y, cardW, cardH, 6, 'rgba(139, 92, 246, 0.05)', 'rgba(139, 92, 246, 0.2)', 1);
      if (team) {
        const flag = flagImages[team.code];
        if (flag) {
          ctx.drawImage(flag, x + 10, y + 12, 26, 17);
          ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
          ctx.strokeRect(x + 10, y + 12, 26, 17);
        }
        ctx.textAlign = 'left';
        ctx.fillStyle = '#ffffff';
        ctx.font = 'bold 13px "Microsoft YaHei", "Segoe UI", sans-serif';
        ctx.fillText(team.name, x + 44, y + 25);
      } else {
        ctx.textAlign = 'center';
        ctx.fillStyle = '#64748b';
        ctx.font = '13px "Microsoft YaHei", sans-serif';
        ctx.fillText('待定', x + cardW / 2, y + 25);
      }
    });

    // Quarterfinalists (8强席位)
    drawSectionTitle('8 强 席 位', '#22d3ee', 'rgba(6, 182, 212, 0.15)', 'rgba(6, 182, 212, 0.35)', 100, 645, 90);

    const qfRows = [qfs.slice(0, 4), qfs.slice(4, 8)];
    qfRows.forEach((rowTeams, rIdx) => {
      const y = 680 + rIdx * 50;

      rowTeams.forEach((team, idx) => {
        const x = 100 + idx * (cardW + cardGap);
        drawRoundedRect(ctx, x, y, cardW, cardH, 6, 'rgba(6, 182, 212, 0.04)', 'rgba(6, 182, 212, 0.18)', 1);

        if (team) {
          const flag = flagImages[team.code];
          if (flag) {
            ctx.drawImage(flag, x + 10, y + 12, 26, 17);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
            ctx.strokeRect(x + 10, y + 12, 26, 17);
          }
          ctx.textAlign = 'left';
          ctx.fillStyle = '#ffffff';
          ctx.font = 'bold 13px "Microsoft YaHei", "Segoe UI", sans-serif';
          ctx.fillText(team.name, x + 44, y + 25);
        } else {
          ctx.textAlign = 'center';
          ctx.fillStyle = '#64748b';
          ctx.font = '13px "Microsoft YaHei", sans-serif';
          ctx.fillText('待定', x + cardW / 2, y + 25);
        }
      });
    });

    // Best Thirds (最佳小组第三)
    drawSectionTitle('最佳小组第三', '#94a3b8', 'rgba(255, 255, 255, 0.06)', 'rgba(255, 255, 255, 0.15)', 100, 795, 110);

    const thirdRows = [thirds.slice(0, 4), thirds.slice(4, 8)];
    thirdRows.forEach((rowTeams, rIdx) => {
      const y = 830 + rIdx * 50;

      rowTeams.forEach((team, idx) => {
        const x = 100 + idx * (cardW + cardGap);
        drawRoundedRect(ctx, x, y, cardW, cardH, 6, 'rgba(255, 255, 255, 0.02)', 'rgba(255, 255, 255, 0.08)', 1);

        if (team) {
          const flag = flagImages[team.code];
          if (flag) {
            ctx.drawImage(flag, x + 10, y + 12, 26, 17);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
            ctx.strokeRect(x + 10, y + 12, 26, 17);
          }
          ctx.textAlign = 'left';
          ctx.fillStyle = '#94a3b8';
          ctx.font = 'bold 13px "Microsoft YaHei", "Segoe UI", sans-serif';
          ctx.fillText(team.name, x + 44, y + 25);
        } else {
          ctx.textAlign = 'center';
          ctx.fillStyle = '#64748b';
          ctx.font = '13px "Microsoft YaHei", sans-serif';
          ctx.fillText('未定', x + cardW / 2, y + 25);
        }
      });
    });

    // Footer
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.06)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(100, 955);
    ctx.lineTo(700, 955);
    ctx.stroke();

    ctx.textAlign = 'left';
    ctx.fillStyle = '#4b5563';
    ctx.font = '12px "Microsoft YaHei", "Segoe UI", sans-serif';
    ctx.fillText('2026 美加墨世界杯预测平台 | 智能多功能工具箱', 100, 980);

    ctx.textAlign = 'right';
    ctx.fillText('由 AI 引擎与用户协作生成', 700, 980);

    // Save browser file
    const dataUrl = canvas.toDataURL('image/png');
    const a = document.createElement('a');
    a.href = dataUrl;
    a.setAttribute('download', `美加墨世界杯预测报告_${champ?.name || '冠军'}.png`);
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

  } catch (error) {
    console.error('Error generating prediction image:', error);
    alert('生成图片失败，请重试！');
  } finally {
    isExporting.value = false;
  }
};

// Export prediction JSON
const downloadJSON = () => {
  const reportData = {
    predictionDate: new Date().toISOString(),
    champion: championTeam.value,
    runnerUp: runnerUpTeam.value,
    thirdPlace: thirdPlaceWinnerTeam.value,
    semifinalists: semifinalists.value,
    quarterfinalists: quarterfinalists.value,
    bestThirds: manualQualifiedThirds.value.map(id => getTeam(id)),
    groupStage: groups.value.map(g => ({
      group: g.name,
      predictionMode: g.predictionMode,
      standings: g.standings.map(s => ({ team: s.id, pts: s.pts, gd: s.gd, gs: s.gs })),
      matches: g.matches
    }))
  };

  const blob = new Blob([JSON.stringify(reportData, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.setAttribute('download', `美加墨世界杯预测报告_${championTeam.value?.name || '冠军'}.json`);
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
};

// ==========================================
// LOCAL STORAGE PERSISTENCE
// ==========================================
const saveProgress = () => {
  const state = {
    currentPhase: currentPhase.value,
    groups: groups.value.map(g => ({
      id: g.id,
      predictionMode: g.predictionMode,
      standings: g.standings,
      matches: g.matches
    })),
    manualQualifiedThirds: manualQualifiedThirds.value,
    knockoutMatches: knockoutMatches.value
  };
  localStorage.setItem('world_cup_predictor_state', JSON.stringify(state));
};

const loadProgress = () => {
  const data = localStorage.getItem('world_cup_predictor_state');
  if (data) {
    try {
      const state = JSON.parse(data);
      currentPhase.value = state.currentPhase || 'group';
      manualQualifiedThirds.value = state.manualQualifiedThirds || [];
      
      if (state.groups) {
        state.groups.forEach(savedG => {
          const g = groups.value.find(x => x.id === savedG.id);
          if (g) {
            g.predictionMode = savedG.predictionMode;
            g.standings = savedG.standings;
            // Overwrite match dates with the real ones, preserving score inputs
            g.matches = (savedG.matches || []).map((m, idx) => ({
              ...m,
              date: getMatchDate(g.id, idx)
            }));
          }
        });
      }

      if (state.knockoutMatches) {
        knockoutMatches.value = state.knockoutMatches;
      }
    } catch (e) {
      console.error('Error loading prediction state cache:', e);
      initializeGroupsData();
    }
  } else {
    initializeGroupsData();
  }
  calculateProbabilities();
};

onMounted(() => {
  loadProgress();
});
</script>

<style scoped>
.world-cup-predictor-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
  position: relative;
}

/* Header Banner Styling */
.predictor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 20px;
  background: linear-gradient(135deg, rgba(15, 21, 36, 0.9) 0%, rgba(139, 92, 246, 0.08) 100%) !important;
  border-color: rgba(139, 92, 246, 0.25);
  flex-wrap: wrap;
}

.header-left h2 {
  font-size: 1.6rem;
  color: var(--text-primary);
  margin: 6px 0 4px;
}

.header-left p {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.header-actions {
  display: flex;
  gap: 12px;
}

/* Tabs navigation bar */
.phase-navigation-bar {
  display: flex;
  padding: 8px !important;
  border-radius: var(--border-radius-md);
  gap: 4px;
  overflow-x: auto;
}

.phase-nav-btn {
  flex: 1;
  background: transparent;
  border: none;
  padding: 10px 14px;
  border-radius: 8px;
  color: var(--text-secondary);
  font-size: 0.9rem;
  font-weight: 600;
  transition: all 0.25s ease;
  min-width: 140px;
}

.phase-nav-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.03);
  color: var(--text-primary);
}

.phase-nav-btn.active {
  background: var(--bg-tertiary);
  color: var(--accent-purple);
  border: 1px solid rgba(139, 92, 246, 0.2);
}

.phase-nav-btn.completed {
  color: var(--accent-success);
}

.phase-icon {
  margin-right: 6px;
}

.status-badge {
  margin-left: 6px;
  font-size: 0.75rem;
  background: rgba(16, 185, 129, 0.15);
  padding: 1px 5px;
  border-radius: 50%;
  color: var(--accent-success);
}

.phase-nav-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

/* Section labels inside stage views */
.stage-section-header {
  margin-bottom: 24px;
}

.stage-section-header h3 {
  font-size: 1.25rem;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.section-desc {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

/* GROUP STAGE STYLING */
.groups-grid {
  display: grid;
  grid-template-columns: repeat(1, 1fr);
  gap: 24px;
}

@media (min-width: 768px) {
  .groups-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1200px) {
  .groups-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

.group-card {
  display: flex;
  flex-direction: column;
  background: rgba(15, 21, 36, 0.5) !important;
  border-color: rgba(255, 255, 255, 0.04);
}

.group-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 8px;
}

.group-card-header h4 {
  font-size: 1.1rem;
  color: var(--text-primary);
}

.btn-mini-action {
  font-size: 0.7rem;
  background: rgba(255, 255, 255, 0.04);
  color: var(--text-secondary);
  border: 1px solid var(--border-color);
  padding: 4px 8px;
  border-radius: 4px;
}

.btn-mini-action:hover:not(:disabled) {
  background: rgba(139, 92, 246, 0.1);
  color: var(--text-primary);
  border-color: rgba(139, 92, 246, 0.3);
}

/* Table layout Inside cards */
.standings-table-wrapper {
  overflow-x: auto;
}

.standings-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.825rem;
}

.standings-table th, .standings-table td {
  padding: 8px 6px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.03);
}

.standings-table th {
  color: var(--text-muted);
  font-weight: 600;
  text-transform: uppercase;
  font-size: 0.725rem;
}

.col-rank {
  width: 28px;
}

.col-team {
  min-width: 110px;
}

.col-stat {
  width: 44px;
  text-align: center;
  font-family: var(--font-mono);
}

.col-order {
  width: 56px;
  text-align: center;
}

.rank-number {
  display: inline-flex;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  align-items: center;
  justify-content: center;
  font-size: 0.7rem;
  font-weight: 600;
  background: rgba(255, 255, 255, 0.05);
}

.rank-1 {
  background: rgba(16, 185, 129, 0.15);
  color: var(--accent-success);
}

.rank-2 {
  background: rgba(6, 182, 212, 0.15);
  color: var(--accent-cyan);
}

.rank-3 {
  background: rgba(139, 92, 246, 0.1);
  color: var(--accent-purple);
}

.qualifies-direct {
  background: rgba(16, 185, 129, 0.04) !important;
}

.qualifies-third {
  background: rgba(139, 92, 246, 0.03) !important;
}

.standings-table tbody tr {
  transition: background-color 0.2s ease;
}

.standings-table tbody tr:hover {
  background: rgba(255, 255, 255, 0.02) !important;
}

/* Country flag styling */
.team-flag-img {
  width: 22px;
  height: 15px;
  object-fit: cover;
  border-radius: 2px;
  vertical-align: middle;
  margin-right: 8px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  display: inline-block;
}

.team-flag-img.inline-flag {
  margin-right: 6px;
}

.team-flag-img-huge {
  width: 80px;
  height: 52px;
  object-fit: cover;
  border-radius: 6px;
  margin-bottom: 8px;
  border: 2px solid rgba(255, 255, 255, 0.2);
  display: block;
}

.block-center {
  margin-left: auto;
  margin-right: auto;
}

.vertical-middle {
  vertical-align: middle;
}

.team-name-text {
  vertical-align: middle;
  color: var(--text-primary);
}

.host-badge, .champ-badge {
  font-size: 0.65rem;
  padding: 1px 3px;
  border-radius: 3px;
  margin-left: 4px;
  vertical-align: middle;
}

.host-badge {
  background: rgba(6, 182, 212, 0.15);
  color: var(--accent-cyan);
}

.champ-badge {
  background: rgba(253, 186, 116, 0.15);
  color: #fdba74;
}

.positive-gd {
  color: var(--accent-success);
}

.negative-gd {
  color: var(--accent-error);
}

/* Ranking arrow buttons */
.order-buttons {
  display: flex;
  gap: 2px;
  justify-content: center;
}

.order-arrow {
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--border-color);
  color: var(--text-secondary);
  font-size: 0.65rem;
  padding: 2px 5px;
  border-radius: 3px;
  cursor: pointer;
}

.order-arrow:hover:not(:disabled) {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.order-arrow:disabled {
  opacity: 0.2;
  cursor: not-allowed;
}

/* Match entries style */
.group-mode-actions {
  margin-top: 10px;
  text-align: center;
}

.btn-text-only {
  background: transparent;
  border: none;
  font-size: 0.75rem;
  color: var(--accent-cyan);
  cursor: pointer;
  padding: 4px 8px;
}

.btn-text-only:hover {
  color: #22d3ee;
}

.group-matches-drawer {
  margin-top: 12px;
  border-top: 1px dashed var(--border-color);
  padding-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.match-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: rgba(8, 11, 17, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.02);
  border-radius: 6px;
  padding: 6px 8px;
  font-size: 0.75rem;
}

.match-date-badge {
  font-family: var(--font-mono);
  font-size: 0.65rem;
  color: var(--text-muted);
  background: rgba(255, 255, 255, 0.03);
  padding: 2px 6px;
  border-radius: 4px;
  margin-right: 6px;
  flex-shrink: 0;
}

.match-team {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.match-team.home {
  justify-content: flex-end;
  text-align: right;
}

.match-team.away {
  justify-content: flex-start;
  text-align: left;
}

.match-team .team-name {
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.match-score-inputs {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 0 8px;
}

.score-input {
  width: 32px;
  height: 24px;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  color: var(--text-primary);
  text-align: center;
  font-family: var(--font-mono);
  font-size: 0.8rem;
  outline: none;
  -moz-appearance: textfield;
}

.score-input::-webkit-outer-spin-button,
.score-input::-webkit-inner-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.score-input:focus {
  border-color: var(--accent-purple);
}

.score-vs {
  color: var(--text-muted);
}

.stage-footer-actions {
  margin-top: 36px;
  display: flex;
  justify-content: flex-end;
  gap: 16px;
  border-top: 1px solid var(--border-color);
  padding-top: 24px;
}

/* Transiton style for matching drawer */
.slide-drawer-enter-active, .slide-drawer-leave-active {
  transition: all 0.3s ease;
  max-height: 250px;
  overflow: hidden;
}

.slide-drawer-enter-from, .slide-drawer-leave-to {
  max-height: 0;
  opacity: 0;
  overflow: hidden;
}

/* PHASE 2: THIRD PLACE COMPONENT */
.third-place-layout {
  display: grid;
  grid-template-columns: 1fr;
  gap: 24px;
}

@media (min-width: 1024px) {
  .third-place-layout {
    grid-template-columns: 1fr 340px;
  }
}

.third-comparison-card {
  background: rgba(15, 21, 36, 0.4) !important;
}

.card-inner-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.comparison-table th, .comparison-table td {
  padding: 10px 8px;
}

.col-group {
  width: 60px;
  color: var(--text-secondary);
}

.col-status {
  width: 80px;
  text-align: center;
}

.col-manual-toggle {
  width: 80px;
  text-align: center;
}

.status-indicator-badge {
  font-size: 0.7rem;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: 600;
}

.status-indicator-badge.pass {
  background: rgba(16, 185, 129, 0.1);
  color: var(--accent-success);
}

.status-indicator-badge.fail {
  background: rgba(239, 68, 68, 0.1);
  color: var(--accent-error);
}

.third-qualified {
  background: rgba(16, 185, 129, 0.02) !important;
}

.third-eliminated {
  opacity: 0.6;
}

.custom-checkbox {
  width: 16px;
  height: 16px;
  accent-color: var(--accent-purple);
  cursor: pointer;
}

.third-info-card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  background: rgba(15, 21, 36, 0.6) !important;
}

.third-info-card h4 {
  color: var(--text-primary);
  font-size: 1rem;
}

.third-info-card p, .third-info-card li {
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.5;
}

.third-info-card ul {
  padding-left: 16px;
}

.selection-counter-card {
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  padding: 12px;
  border-radius: var(--border-radius-sm);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.counter-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.counter-value {
  font-family: var(--font-mono);
  font-size: 1.15rem;
  font-weight: 700;
  color: var(--accent-error);
}

.counter-value.correct {
  color: var(--accent-success);
}

.warning-text-hint {
  font-size: 0.725rem;
  color: #fbbf24;
}

/* PHASE 3: TOURNAMENT BRACKET LAYOUT */
.bracket-navigation-controls {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 16px;
}

.bracket-viewport {
  width: 100%;
  overflow-x: auto;
  border: 1px solid var(--border-color);
  background: rgba(5, 7, 12, 0.8);
  border-radius: var(--border-radius-lg);
  box-shadow: inset 0 0 40px rgba(0, 0, 0, 0.8);
}

.bracket-canvas {
  display: flex;
  justify-content: space-between;
  width: 1540px; /* Force wide width to layout rounds side-by-side */
  padding: 40px 20px;
  min-height: 720px;
}

.bracket-half {
  display: flex;
  width: 620px;
  justify-content: space-between;
}

.bracket-round {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  width: 130px;
  height: 640px;
}

.round-title {
  text-align: center;
  font-size: 0.7rem;
  color: var(--text-muted);
  font-weight: 700;
  text-transform: uppercase;
  margin-bottom: 8px;
}

.match-cards-list {
  display: flex;
  flex-direction: column;
  justify-content: space-around;
  height: 100%;
}

.bracket-match-card {
  background: rgba(15, 21, 36, 0.75);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  overflow: hidden;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
  transition: all 0.2s ease;
}

.bracket-match-card:hover {
  border-color: rgba(255, 255, 255, 0.1);
  box-shadow: 0 6px 15px rgba(0,0,0,0.3);
}

.team-slot {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  cursor: pointer;
  font-size: 0.75rem;
  position: relative;
  transition: all 0.2s ease;
  user-select: none;
}

.team-slot:hover {
  background: rgba(255, 255, 255, 0.04);
}

.team-slot .team-name {
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 68px;
}

.team-slot .team-name.color-muted {
  color: var(--text-muted);
}

.team-slot:hover .team-name {
  color: var(--text-primary);
}

.seed-text {
  position: absolute;
  right: 6px;
  font-family: var(--font-mono);
  font-size: 0.55rem;
  color: var(--text-muted);
  background: rgba(255,255,255,0.03);
  padding: 1px 3px;
  border-radius: 2px;
}

/* Bracket win/loss markers */
.team-slot.winner {
  background: rgba(139, 92, 246, 0.15);
}

.team-slot.winner .team-name {
  color: var(--accent-purple);
  font-weight: 700;
}

.team-slot.loser {
  opacity: 0.4;
}

/* Interactive connection hover highlights */
.team-slot.highlighted {
  box-shadow: inset 0 0 10px rgba(6, 182, 212, 0.25);
  background: rgba(6, 182, 212, 0.04);
}

.team-slot.highlighted .team-name {
  color: var(--accent-cyan);
}

/* Tree progression alignment sizes */
.r-32 {
  width: 140px;
}

.r-16 {
  width: 130px;
}

.r-qf {
  width: 120px;
}

.r-sf {
  width: 110px;
}

/* CENTRAL AREA (FINALS) */
.bracket-center-stage {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 250px;
}

.final-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  position: relative;
}

.trophy-glow-bg {
  font-size: 4.5rem;
  filter: drop-shadow(0 0 20px rgba(251, 191, 36, 0.2));
  opacity: 0.15;
  position: absolute;
  top: -30px;
  z-index: 0;
  animation: floatTrophy 3s ease-in-out infinite;
}

@keyframes floatTrophy {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}

.match-title-center {
  font-size: 0.75rem;
  color: #fbbf24;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  margin-bottom: 12px;
  z-index: 1;
}

.final-match-card {
  width: 210px;
  padding: 16px !important;
  background: rgba(15, 21, 36, 0.8) !important;
  border-color: rgba(251, 191, 36, 0.25);
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5), 0 0 20px rgba(251, 191, 36, 0.05);
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.final-team-row {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 10px 6px;
  cursor: pointer;
  border-radius: 6px;
  transition: all 0.2s ease;
  user-select: none;
}

.final-team-row:hover {
  background: rgba(255, 255, 255, 0.04);
}

.team-name-huge {
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 150px;
  margin-top: 4px;
}

.final-team-row .crown-icon {
  font-size: 1.15rem;
  margin-top: 2px;
}

.final-team-row:hover .team-name-huge {
  color: var(--text-primary);
}

.final-team-row.winner {
  background: rgba(251, 191, 36, 0.1);
  border: 1px solid rgba(251, 191, 36, 0.25);
}

.final-team-row.winner .team-name-huge {
  color: #fbbf24;
  font-weight: 700;
}

.final-team-row.loser {
  opacity: 0.4;
}

.final-team-row.highlighted {
  box-shadow: inset 0 0 8px rgba(6, 182, 212, 0.25);
}

.final-vs-divider {
  font-size: 0.7rem;
  color: var(--text-muted);
  font-weight: 700;
  text-align: center;
  position: relative;
  margin: 4px 0;
}

/* 3rd Place Playoff UI */
.third-place-playoff-box {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  align-items: center;
  z-index: 1;
}

.third-match-title {
  font-size: 0.65rem;
  color: var(--text-muted);
  text-transform: uppercase;
  margin-bottom: 6px;
  font-weight: 600;
}

.third-playoff-card {
  display: flex;
  align-items: center;
  background: rgba(15, 21, 36, 0.6);
  border: 1px solid var(--border-color);
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 0.7rem;
  gap: 8px;
}

.third-team-slot {
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  color: var(--text-secondary);
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 4px;
}

.third-team-slot:hover {
  background: rgba(255,255,255,0.03);
  color: var(--text-primary);
}

.third-team-slot.winner {
  color: #f59e0b;
  font-weight: 600;
  background: rgba(245, 158, 11, 0.1);
}

.third-playoff-card .vs {
  color: var(--text-muted);
}

/* Dynamic Champion Box Overlay card */
.champion-announcement {
  margin-top: 30px;
  width: 220px;
  padding: 16px !important;
  background: linear-gradient(135deg, rgba(15, 21, 36, 0.9) 0%, rgba(251, 191, 36, 0.08) 100%) !important;
  border-color: rgba(251, 191, 36, 0.3) !important;
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.5), 0 0 20px rgba(251, 191, 36, 0.1);
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  z-index: 2;
  gap: 8px;
}

.celebration-ribbon {
  font-size: 0.65rem;
  background: rgba(251, 191, 36, 0.15);
  color: #fbbf24;
  padding: 3px 8px;
  border-radius: 12px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.champion-announcement h3 {
  font-size: 1.15rem;
  color: #fbbf24;
}

.champion-announcement p {
  font-size: 0.65rem;
  color: var(--text-secondary);
}

/* Transition for champion popup */
.pop-champion-enter-active {
  animation: popChamp 0.45s cubic-bezier(0.175, 0.885, 0.32, 1.275) forwards;
}

@keyframes popChamp {
  0% { transform: scale(0.6); opacity: 0; }
  100% { transform: scale(1); opacity: 1; }
}

/* Confetti overlay canvas */
.confetti-canvas-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
  z-index: 9999;
}

/* PHASE 4: FINAL REPORT VIEW */
.report-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 24px;
}

@media (min-width: 768px) {
  .report-grid {
    grid-template-columns: 320px 1fr;
  }
}

.report-champion-card {
  background: linear-gradient(135deg, rgba(15, 21, 36, 0.8) 0%, rgba(251, 191, 36, 0.06) 100%) !important;
  border-color: rgba(251, 191, 36, 0.2);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 24px !important;
}

.stars-decorator {
  font-size: 0.85rem;
  letter-spacing: 4px;
}

.winner-title-text {
  font-size: 1.8rem;
  color: #fbbf24;
  margin-bottom: 6px;
}

.winner-subtitle-text {
  font-size: 0.75rem;
  color: var(--text-secondary);
  margin-bottom: 24px;
}

.runner-up-box {
  display: flex;
  font-size: 0.8rem;
  background: rgba(0,0,0,0.2);
  border: 1px solid var(--border-color);
  padding: 6px 12px;
  border-radius: 6px;
  width: 85%;
  align-items: center;
  justify-content: center;
  margin-bottom: 8px;
  gap: 6px;
}

.runner-up-box .label {
  color: var(--text-muted);
}

.runner-up-box .value {
  color: var(--text-primary);
  font-weight: 600;
}

.runner-up-box .team-flag-img {
  margin-right: 0;
}

.report-details-card {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.road-step {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border-bottom: 1px solid var(--border-color);
  padding-bottom: 16px;
}

.step-badge {
  font-size: 0.725rem;
  font-weight: 700;
  color: var(--text-secondary);
  text-transform: uppercase;
  background: var(--bg-tertiary);
  padding: 2px 8px;
  border-radius: 4px;
  align-self: flex-start;
}

.team-tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.mini-team-tag {
  font-size: 0.75rem;
  background: rgba(139, 92, 246, 0.1);
  color: var(--accent-purple);
  border: 1px solid rgba(139, 92, 246, 0.2);
  padding: 4px 10px;
  border-radius: 20px;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.mini-team-tag.secondary {
  background: rgba(6, 182, 212, 0.1);
  color: var(--accent-cyan);
  border-color: rgba(6, 182, 212, 0.2);
}

.mini-team-tag.info {
  background: rgba(255, 255, 255, 0.03);
  color: var(--text-secondary);
  border-color: var(--border-color);
}

.report-share-actions {
  display: flex;
  gap: 12px;
  margin-top: 12px;
}
.probability-badge {
  font-family: var(--font-mono);
  font-size: 0.725rem;
  font-weight: 700;
  color: var(--accent-cyan);
  background: rgba(6, 182, 212, 0.1);
  padding: 2px 6px;
  border-radius: 4px;
}

/* AI Simulation Modal Style */
.ai-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(4, 6, 12, 0.8);
  backdrop-filter: blur(10px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10000;
  padding: 20px;
}

.ai-modal-card {
  width: 100%;
  max-width: 600px;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  background: linear-gradient(135deg, rgba(15, 21, 36, 0.95) 0%, rgba(139, 92, 246, 0.08) 100%) !important;
  border-color: rgba(139, 92, 246, 0.3) !important;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.6), 0 0 30px rgba(139, 92, 246, 0.15) !important;
  border-radius: 8px;
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
}

.modal-header h3 {
  font-size: 1.2rem;
  color: var(--text-primary);
  margin: 0;
  flex: 1;
}

.modal-icon {
  font-size: 1.5rem;
}

.btn-close-modal {
  background: transparent;
  border: none;
  font-size: 1.8rem;
  color: var(--text-muted);
  cursor: pointer;
  line-height: 1;
  padding: 0;
  transition: color 0.2s ease;
}

.btn-close-modal:hover {
  color: var(--text-primary);
}

.modal-body {
  padding: 20px;
  overflow-y: auto;
  flex: 1;
  max-height: 55vh;
}

.scrollable-content::-webkit-scrollbar {
  width: 6px;
}

.scrollable-content::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.01);
}

.scrollable-content::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.15);
  border-radius: 3px;
}

.scrollable-content::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}

.summary-markdown-view {
  font-size: 0.9rem;
  color: var(--text-secondary);
  line-height: 1.6;
}

.summary-markdown-view h1, .summary-markdown-view h2, .summary-markdown-view h3 {
  color: var(--text-primary);
  margin-top: 16px;
  margin-bottom: 8px;
}

.summary-markdown-view h1 { font-size: 1.4rem; border-bottom: 1px solid rgba(255, 255, 255, 0.08); padding-bottom: 6px; }
.summary-markdown-view h2 { font-size: 1.2rem; }
.summary-markdown-view h3 { font-size: 1.05rem; }

.summary-markdown-view p {
  margin-bottom: 12px;
}

.summary-markdown-view strong {
  color: #fbbf24;
}

.summary-markdown-view ul {
  padding-left: 20px;
  margin-bottom: 12px;
}

.summary-markdown-view li {
  margin-bottom: 6px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 16px 20px;
  border-top: 1px solid rgba(255, 255, 255, 0.08);
  background: rgba(8, 11, 17, 0.3);
}

.btn-confirm-simulate {
  background: linear-gradient(135deg, var(--accent-purple) 0%, #a855f7 100%) !important;
  color: white !important;
  border: 1px solid rgba(168, 85, 247, 0.3) !important;
  box-shadow: 0 4px 15px rgba(139, 92, 246, 0.3) !important;
}

.btn-confirm-simulate:hover {
  box-shadow: 0 6px 20px rgba(139, 92, 246, 0.5) !important;
}

/* Modal Transition */
.fade-scale-enter-active, .fade-scale-leave-active {
  transition: all 0.3s ease;
}

.fade-scale-enter-from, .fade-scale-leave-to {
  opacity: 0;
}

.fade-scale-enter-active .ai-modal-card {
  animation: modalScaleIn 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

.fade-scale-leave-active .ai-modal-card {
  transform: scale(0.95);
  opacity: 0;
  transition: all 0.2s ease;
}

@keyframes modalScaleIn {
  from { transform: scale(0.9); opacity: 0; }
  to { transform: scale(1); opacity: 1; }
}
</style>
