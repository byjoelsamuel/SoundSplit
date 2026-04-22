import './style.css'

// ── FREE TRACKS (Free Music Archive - CC licensed) ──────
const defaultTracks = [
  {
    name: 'Song Name',
    artist: 'Placeholder',
    url: '/song1.mp3',
    source: 'LOCAL'
  },
  {
    name: 'Song 2',
    artist: 'Artist',
    url: '/song2.mp3',
    source: 'LOCAL'
  }
];

// ── STATE ───────────────────────────────────────────────
let tracks = [...defaultTracks];
let currentIndex = 0;
let isPlaying = false;
const audio = new Audio();
audio.volume = 0.8;

// ── BUILD UI ────────────────────────────────────────────
document.querySelector('#app').innerHTML = `
<div class="ipod">
  <div class="ipod-logo">MELODIC</div>

  <div class="screen-bezel">
    <div class="screen">
      <div class="screen-header">
        <span id="status">⏸ PAUSED</span>
        <div class="battery">
          <span style="font-size:11px">🔋</span>
          <div class="battery-bar"></div>
        </div>
        <span id="clock">--:--</span>
      </div>
      <div class="screen-body">
        <div class="now-playing-label">◈ now playing ◈</div>
        <div class="track-title" id="title">MELODIC PLAYER</div>
        <div class="track-artist" id="artist">select a track</div>
        <div class="track-source" id="source">◆ LOCAL</div>
        <div class="progress-bar-container" id="progressContainer">
          <div class="progress-bar" id="progress"></div>
        </div>
        <div class="time-row">
          <span id="currentTime">0:00</span>
          <span id="trackNum">0 / 0</span>
          <span id="duration">0:00</span>
        </div>
      </div>
    </div>
  </div>

  <div class="wheel-container">
    <div class="wheel">
      <span class="wheel-label top">MENU</span>
      <span class="wheel-label bottom">▶▶</span>
      <span class="wheel-label left">◀◀</span>
      <span class="wheel-label right">▶▶</span>
      <button class="wheel-btn top"    id="btnMenu"  aria-label="menu"></button>
      <button class="wheel-btn bottom" id="btnNext"  aria-label="next track"></button>
      <button class="wheel-btn left"   id="btnPrev"  aria-label="previous track"></button>
      <button class="wheel-btn right"  id="btnNext2" aria-label="next track"></button>
      <button class="center-btn" id="btnPlay" aria-label="play pause">▶</button>
    </div>
  </div>
</div>

<div class="volume-row">
  <span>🔈</span>
  <input type="range" id="volume" min="0" max="1" step="0.01" value="0.8" aria-label="volume">
  <span>🔊</span>
</div>

<div class="tabs">
  <button class="tab-btn active" data-tab="library">LIBRARY</button>
  <button class="tab-btn" data-tab="soundcloud">SOUNDCLOUD</button>
  <button class="tab-btn" data-tab="files">FILES</button>
</div>

<div class="panel active" id="panel-library">
  <div class="playlist" id="playlist"></div>
</div>

<div class="panel" id="panel-soundcloud">
  <div class="sc-panel">
    <div class="sc-input-row">
      <input class="sc-input" id="scUrl" placeholder="paste soundcloud url..." />
      <button class="sc-load-btn" id="scLoad">LOAD</button>
    </div>
    <iframe id="scFrame" height="120" scrolling="no" frameborder="no" allow="autoplay"></iframe>
    <div class="sc-hint">paste any soundcloud.com track or playlist URL above ↑<br>plays directly in the player</div>
  </div>
</div>

<div class="panel" id="panel-files">
  <label class="upload-btn">
    + load music files (mp3, wav, flac)
    <input type="file" id="fileInput" accept="audio/*" multiple hidden />
  </label>
</div>
`;

// ── CLOCK ───────────────────────────────────────────────
function updateClock() {
  const now = new Date();
  document.getElementById('clock').textContent =
    now.getHours().toString().padStart(2,'0') + ':' +
    now.getMinutes().toString().padStart(2,'0');
}
updateClock();
setInterval(updateClock, 10000);

// ── HELPERS ─────────────────────────────────────────────
function fmt(s) {
  if (!s || isNaN(s)) return '0:00';
  return `${Math.floor(s/60)}:${Math.floor(s%60).toString().padStart(2,'0')}`;
}

function setPlaying(state) {
  isPlaying = state;
  document.getElementById('btnPlay').textContent = state ? '⏸' : '▶';
  document.getElementById('status').textContent  = state ? '▶ PLAYING' : '⏸ PAUSED';
}

function loadTrack(index) {
  if (!tracks.length) return;
  currentIndex = index;
  const t = tracks[index];
  audio.src = t.url;
  document.getElementById('title').textContent  = t.name.toUpperCase();
  document.getElementById('artist').textContent = t.artist || '—';
  document.getElementById('source').textContent = `◆ ${t.source || 'LOCAL'}`;
  document.getElementById('progress').style.width = '0%';
  document.getElementById('currentTime').textContent = '0:00';
  document.getElementById('duration').textContent    = '0:00';
  document.getElementById('trackNum').textContent    = `${index+1} / ${tracks.length}`;
  renderPlaylist();
  if (isPlaying) audio.play().catch(()=>{});
}

function renderPlaylist() {
  const pl = document.getElementById('playlist');
  pl.innerHTML = tracks.map((t, i) => `
    <div class="playlist-item ${i===currentIndex?'active':''}" data-i="${i}">
      <span class="num">${String(i+1).padStart(2,'0')}</span>
      <div class="info">
        <div class="name">${t.name}</div>
        <div class="sub">${t.artist||''} ${t.source?'· '+t.source:''}</div>
      </div>
    </div>
  `).join('');
  pl.querySelectorAll('.playlist-item').forEach(el => {
    el.addEventListener('click', () => { isPlaying = true; loadTrack(+el.dataset.i); });
  });
}

// ── AUDIO EVENTS ────────────────────────────────────────
audio.addEventListener('timeupdate', () => {
  if (!audio.duration) return;
  document.getElementById('progress').style.width = (audio.currentTime/audio.duration*100)+'%';
  document.getElementById('currentTime').textContent = fmt(audio.currentTime);
  document.getElementById('duration').textContent    = fmt(audio.duration);
});

audio.addEventListener('ended', () => {
  if (currentIndex < tracks.length-1) loadTrack(currentIndex+1);
  else setPlaying(false);
});

// ── SEEK ────────────────────────────────────────────────
document.getElementById('progressContainer').addEventListener('click', e => {
  if (!audio.duration) return;
  const r = e.currentTarget.getBoundingClientRect();
  audio.currentTime = ((e.clientX-r.left)/r.width)*audio.duration;
});

// ── CONTROLS ────────────────────────────────────────────
document.getElementById('btnPlay').addEventListener('click', () => {
  if (!tracks.length) return;
  if (isPlaying) { audio.pause(); setPlaying(false); }
  else { audio.play().catch(()=>{}); setPlaying(true); }
});

document.getElementById('btnNext').addEventListener('click', () => {
  if (currentIndex < tracks.length-1) { isPlaying=true; loadTrack(currentIndex+1); }
});
document.getElementById('btnNext2').addEventListener('click', () => {
  if (currentIndex < tracks.length-1) { isPlaying=true; loadTrack(currentIndex+1); }
});
document.getElementById('btnPrev').addEventListener('click', () => {
  if (audio.currentTime > 3) audio.currentTime = 0;
  else if (currentIndex > 0) { isPlaying=true; loadTrack(currentIndex-1); }
});

document.getElementById('btnMenu').addEventListener('click', () => {
  document.querySelector('.tabs').scrollIntoView({ behavior: 'smooth' });
});

// ── VOLUME ──────────────────────────────────────────────
document.getElementById('volume').addEventListener('input', e => {
  audio.volume = e.target.value;
});

// ── TABS ────────────────────────────────────────────────
document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('panel-'+btn.dataset.tab).classList.add('active');
  });
});

// ── SOUNDCLOUD EMBED ────────────────────────────────────
document.getElementById('scLoad').addEventListener('click', () => {
  const url = document.getElementById('scUrl').value.trim();
  if (!url.includes('soundcloud.com')) return;
  const frame = document.getElementById('scFrame');
  frame.src = `https://w.soundcloud.com/player/?url=${encodeURIComponent(url)}&color=%2339ff14&auto_play=true&hide_related=true&show_comments=false&show_user=true&show_reposts=false&show_teaser=false`;
  frame.style.display = 'block';
  document.getElementById('title').textContent  = 'SOUNDCLOUD';
  document.getElementById('artist').textContent = 'streaming...';
  document.getElementById('source').textContent = '◆ SOUNDCLOUD';
  document.getElementById('status').textContent = '▶ PLAYING';
});

// ── FILE LOADER ─────────────────────────────────────────
document.getElementById('fileInput').addEventListener('change', e => {
  const files = Array.from(e.target.files);
  const newTracks = files.map(f => ({
    name: f.name.replace(/\.[^/.]+$/,''),
    artist: 'local file',
    url: URL.createObjectURL(f),
    source: 'LOCAL'
  }));
  tracks = [...tracks, ...newTracks];
  isPlaying = true;
  loadTrack(tracks.length - newTracks.length);
  document.querySelector('[data-tab="library"]').click();
});

// ── INIT ────────────────────────────────────────────────
renderPlaylist();
document.getElementById('trackNum').textContent = `0 / ${tracks.length}`;

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js');
  });
}