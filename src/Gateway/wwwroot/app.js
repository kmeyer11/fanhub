/* ============================================================
   FanHub — app.js
   ============================================================ */

// When served from the gateway, API calls are same-origin.
// For standalone dev, set this to 'http://localhost:5050'
const API = '';

// JWT claim keys emitted by .NET ClaimTypes
const CLAIM_NAMEID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const CLAIM_EMAIL  = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';

// ── State ────────────────────────────────────────────────────
const state = {
  token:         null,
  userId:        null,
  email:         null,
  favoriteTeam:  null,
  team:          null,
  _selectedTeam: null,
};

const loadedTabs = new Set();

// ── Bootstrap ────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  const stored = localStorage.getItem('fanhub_token');
  if (stored) {
    try {
      hydrateState(stored);
      showApp();
    } catch {
      localStorage.removeItem('fanhub_token');
      showAuth();
    }
  } else {
    showAuth();
  }

  wireAuthListeners();
  wireAppListeners();
});

function hydrateState(token) {
  const claims    = parseJwt(token);
  state.token     = token;
  state.userId    = claims[CLAIM_NAMEID];
  state.email     = claims[CLAIM_EMAIL];
  state.favoriteTeam = claims['favorite_team'];
}

// ── JWT helper ───────────────────────────────────────────────
function parseJwt(token) {
  const raw     = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
  const padded  = raw + '='.repeat((4 - raw.length % 4) % 4);
  return JSON.parse(atob(padded));
}

// ── API helper ───────────────────────────────────────────────
async function api(path, opts = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (state.token) headers['Authorization'] = `Bearer ${state.token}`;
  Object.assign(headers, opts.headers ?? {});

  const res = await fetch(API + path, { ...opts, headers });
  if (res.status === 204) return null;
  if (!res.ok) {
    const msg = await res.text().catch(() => res.statusText);
    throw new Error(msg || `HTTP ${res.status}`);
  }
  return res.json();
}

/* ════════════════════════════════════════════════════════════
   AUTH SCREEN
   ════════════════════════════════════════════════════════════ */

function showAuth() {
  document.getElementById('auth-screen').hidden = false;
  document.getElementById('app-screen').hidden  = true;
  loadTeamPicker();
}

function showApp() {
  document.getElementById('auth-screen').hidden = true;
  document.getElementById('app-screen').hidden  = false;
  initDashboard();
}

function wireAuthListeners() {
  document.getElementById('tab-login-btn').addEventListener('click', () => switchAuthTab('login'));
  document.getElementById('tab-register-btn').addEventListener('click', () => switchAuthTab('register'));

  // ── Login ──
  document.getElementById('login-form').addEventListener('submit', async e => {
    e.preventDefault();
    const email    = document.getElementById('login-email').value.trim();
    const password = document.getElementById('login-password').value;
    const btn      = document.getElementById('login-btn');
    const err      = document.getElementById('login-error');

    setLoading(btn, true, 'Signing in…');
    err.style.display = 'none';

    try {
      const data = await api('/users/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      });
      localStorage.setItem('fanhub_token', data.token);
      hydrateState(data.token);
      showApp();
    } catch (ex) {
      showError(err, ex.message || 'Invalid email or password.');
    } finally {
      setLoading(btn, false, 'Sign in');
    }
  });

  // ── Register ──
  document.getElementById('register-form').addEventListener('submit', async e => {
    e.preventDefault();
    const email    = document.getElementById('reg-email').value.trim();
    const password = document.getElementById('reg-password').value;
    const btn      = document.getElementById('register-btn');
    const err      = document.getElementById('register-error');

    if (!state._selectedTeam) {
      showError(err, 'Please choose your favourite team.');
      return;
    }

    setLoading(btn, true, 'Creating account…');
    err.style.display = 'none';

    try {
      await api('/users/register', {
        method: 'POST',
        body: JSON.stringify({ email, password, favoriteTeam: state._selectedTeam }),
      });
      // Auto-login after register
      const data = await api('/users/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      });
      localStorage.setItem('fanhub_token', data.token);
      hydrateState(data.token);
      showApp();
    } catch (ex) {
      showError(err, ex.message || 'Registration failed. Please try again.');
    } finally {
      setLoading(btn, false, 'Create account');
    }
  });
}

function switchAuthTab(tab) {
  document.getElementById('tab-login-btn').classList.toggle('active', tab === 'login');
  document.getElementById('tab-register-btn').classList.toggle('active', tab === 'register');
  document.getElementById('login-form').hidden    = tab !== 'login';
  document.getElementById('register-form').hidden = tab !== 'register';
  document.getElementById('login-error').style.display    = 'none';
  document.getElementById('register-error').style.display = 'none';

  // Reload team picker each time register tab opens so it reflects latest sync
  if (tab === 'register') loadTeamPicker();
}

async function loadTeamPicker() {
  const container = document.getElementById('team-picker-grid');
  try {
    const teams = await api('/teams');
    container.innerHTML = '';

    // Group by league (already ordered league ASC, name ASC from API)
    const leagues = {};
    teams.forEach(t => (leagues[t.league] ??= []).push(t));

    Object.entries(leagues).forEach(([league, members]) => {
      // League header
      const header = document.createElement('div');
      header.className = 'team-picker-league';
      header.textContent = league;
      container.appendChild(header);

      // Team grid for this league
      const row = document.createElement('div');
      row.className = 'team-picker-row';
      members.forEach(team => {
        const el = document.createElement('div');
        el.className = 'team-option';
        el.dataset.name = team.name;
        el.innerHTML = `
          <img src="${esc(team.badgeUrl)}" alt="${esc(team.shortName)}"
               onerror="this.style.display='none'">
          <span>${esc(team.shortName || team.name)}</span>
        `;
        el.addEventListener('click', () => {
          container.querySelectorAll('.team-option').forEach(o => o.classList.remove('selected'));
          el.classList.add('selected');
          state._selectedTeam = team.name;
        });
        row.appendChild(el);
      });
      container.appendChild(row);
    });
  } catch {
    container.innerHTML = '<div class="team-picker-loading">Could not load teams</div>';
  }
}

/* ════════════════════════════════════════════════════════════
   APP / DASHBOARD
   ════════════════════════════════════════════════════════════ */

function wireAppListeners() {
  // Tab buttons
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => activateTab(btn.dataset.tab));
  });

  // Bell → notifications tab
  document.getElementById('notif-btn').addEventListener('click', () => activateTab('notifications'));

  // Logout
  document.getElementById('logout-btn').addEventListener('click', () => {
    Object.assign(state, { token: null, userId: null, email: null,
      favoriteTeam: null, team: null, _selectedTeam: null });
    localStorage.removeItem('fanhub_token');
    resetAccent();
    showAuth();
  });
}

function activateTab(tab) {
  document.querySelectorAll('.tab-btn').forEach(b =>
    b.classList.toggle('active', b.dataset.tab === tab));
  document.querySelectorAll('.tab-panel').forEach(p =>
    p.classList.toggle('active', p.id === `panel-${tab}`));
  loadTab(tab);
}

async function initDashboard() {
  loadedTabs.clear();
  activateTab('fixtures');

  document.getElementById('nav-user').textContent = state.email ?? '';

  try {
    const team = await api(`/teams/${encodeURIComponent(state.favoriteTeam)}`);
    state.team = team;
    renderHero(team);
    extractAndApplyColor(team.badgeUrl);
  } catch {
    document.getElementById('hero-name').textContent = state.favoriteTeam;
    document.getElementById('nav-team').textContent  = state.favoriteTeam;
  }

  pollNotifCount();
}

function renderHero(team) {
  setImg('hero-badge', team.badgeUrl, team.name);
  setImg('nav-badge',  team.badgeUrl, team.name);
  document.getElementById('hero-name').textContent = team.name;
  document.getElementById('nav-team').textContent  = team.name;

  setText('hero-league',  team.league);
  setText('hero-stadium', team.stadium);
  setText('hero-founded', team.founded ? `Est. ${team.founded}` : '');

  // Hide empty meta items
  ['league','stadium','founded'].forEach(key => {
    const wrap = document.getElementById(`hero-${key}-wrap`);
    if (wrap) wrap.hidden = !team[key];
  });
}

/* ════════════════════════════════════════════════════════════
   COLOUR EXTRACTION
   ════════════════════════════════════════════════════════════ */

function extractAndApplyColor(badgeUrl) {
  if (!badgeUrl) { applyAccent(teamHashColor(state.favoriteTeam)); return; }

  const img = new Image();
  img.crossOrigin = 'anonymous';
  img.onload  = () => {
    try   { applyAccent(dominantColor(img)); }
    catch { applyAccent(teamHashColor(state.favoriteTeam)); }
  };
  img.onerror = () => applyAccent(teamHashColor(state.favoriteTeam));
  // Append cache-bust only if needed to trigger CORS preflight
  img.src = badgeUrl;
}

function dominantColor(img) {
  const SIZE = 64;
  const cv   = document.createElement('canvas');
  cv.width   = SIZE;
  cv.height  = SIZE;
  const ctx  = cv.getContext('2d');
  ctx.drawImage(img, 0, 0, SIZE, SIZE);
  const { data } = ctx.getImageData(0, 0, SIZE, SIZE);

  const map = {};
  for (let i = 0; i < data.length; i += 4) {
    const [r, g, b, a] = [data[i], data[i+1], data[i+2], data[i+3]];
    if (a < 100) continue;                              // transparent
    if (r > 230 && g > 230 && b > 230) continue;       // near-white
    if (r < 22  && g < 22  && b < 22)  continue;       // near-black
    const luma = 0.299*r + 0.587*g + 0.114*b;
    if (luma < 28 || luma > 218) continue;              // too dark / too bright
    const saturation = Math.max(r,g,b) - Math.min(r,g,b);
    if (saturation < 30) continue;                      // near-grey
    // Quantise
    const key = `${r>>4<<4},${g>>4<<4},${b>>4<<4}`;
    map[key] = (map[key] ?? 0) + 1;
  }

  const top = Object.entries(map).sort((a, b) => b[1] - a[1])[0];
  if (!top) throw new Error('no usable color');
  const [r, g, b] = top[0].split(',').map(Number);
  return `rgb(${r},${g},${b})`;
}

function applyAccent(color) {
  const r = document.documentElement;
  r.style.setProperty('--accent', color);

  if (color.startsWith('rgb(')) {
    const [rv, gv, bv] = color.match(/\d+/g).map(Number);
    r.style.setProperty('--accent-dark',   `rgb(${Math.round(rv*.65)},${Math.round(gv*.65)},${Math.round(bv*.65)})`);
    r.style.setProperty('--accent-alpha',  `rgba(${rv},${gv},${bv},0.12)`);
    r.style.setProperty('--accent-border', `rgba(${rv},${gv},${bv},0.28)`);
  } else {
    // hsl fallback — just set the rest to the same value
    r.style.setProperty('--accent-dark',   color);
    r.style.setProperty('--accent-alpha',  color.replace('hsl(', 'hsla(').replace(')', ', 0.12)'));
    r.style.setProperty('--accent-border', color.replace('hsl(', 'hsla(').replace(')', ', 0.28)'));
  }
}

function resetAccent() {
  const r = document.documentElement;
  r.style.setProperty('--accent',        '#3b82f6');
  r.style.setProperty('--accent-dark',   '#1d4ed8');
  r.style.setProperty('--accent-alpha',  'rgba(59,130,246,0.12)');
  r.style.setProperty('--accent-border', 'rgba(59,130,246,0.28)');
}

function teamHashColor(name) {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
  const hue = ((Math.abs(h) % 280) + 40) % 360;
  return `hsl(${hue}, 60%, 48%)`;
}

/* ════════════════════════════════════════════════════════════
   TAB LOADING
   ════════════════════════════════════════════════════════════ */

function loadTab(tab) {
  if (loadedTabs.has(tab)) return;
  loadedTabs.add(tab);

  switch (tab) {
    case 'fixtures':      renderFixtures();      break;
    case 'results':       renderResults();       break;
    case 'squad':         renderSquad();         break;
    case 'standings':     renderStandings();     break;
    case 'notifications': renderNotifications(); break;
  }
}

/* ════════════════════════════════════════════════════════════
   FIXTURES
   ════════════════════════════════════════════════════════════ */

async function renderFixtures() {
  const panel = document.getElementById('panel-fixtures');
  panel.innerHTML = spinner();
  try {
    const fixtures = await api(`/fixtures/${encodeURIComponent(state.favoriteTeam)}/upcoming`);
    if (!fixtures.length) { panel.innerHTML = empty('📅', 'No upcoming fixtures'); return; }

    panel.innerHTML = sectionHeader('Upcoming Fixtures', `${fixtures.length} matches`) +
      `<div class="fixture-list">${fixtures.map(f => fixtureCard(f, false)).join('')}</div>`;
  } catch {
    panel.innerHTML = empty('⚠️', 'Could not load fixtures');
  }
}

async function renderResults() {
  const panel = document.getElementById('panel-results');
  panel.innerHTML = spinner();
  try {
    const results = await api(`/fixtures/${encodeURIComponent(state.favoriteTeam)}/history`);
    if (!results.length) { panel.innerHTML = empty('📋', 'No recent results'); return; }

    panel.innerHTML = sectionHeader('Recent Results', `Last ${results.length} matches`) +
      `<div class="fixture-list">${results.map(f => fixtureCard(f, true)).join('')}</div>`;
  } catch {
    panel.innerHTML = empty('⚠️', 'Could not load results');
  }
}

function fixtureCard(f, showScore) {
  const my    = state.favoriteTeam;
  const isH   = f.homeTeam === my;
  const isA   = f.awayTeam === my;
  const d     = new Date(f.matchDate);
  const date  = d.toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' });
  const time  = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });

  let outcome = '';
  if (showScore && (isH || isA)) {
    const mine = isH ? f.homeScore : f.awayScore;
    const opp  = isH ? f.awayScore : f.homeScore;
    if (mine > opp)      outcome = `<div class="result-outcome win">W</div>`;
    else if (mine < opp) outcome = `<div class="result-outcome loss">L</div>`;
    else                 outcome = `<div class="result-outcome draw">D</div>`;
  }

  const center = showScore
    ? `<div class="fixture-score">${f.homeScore ?? '–'}&thinsp;–&thinsp;${f.awayScore ?? '–'}</div>${outcome}`
    : `<div class="fixture-vs">VS</div><div class="fixture-date">${date}<br>${time}</div>`;

  return `
    <div class="fixture-card">
      <div class="fixture-team">
        <div class="team-name ${isH ? 'accent' : ''}">${esc(f.homeTeam)}</div>
        ${isH ? '<div class="fixture-label">Home</div>' : ''}
      </div>
      <div class="fixture-center">
        ${center}
        <div class="fixture-pill">${esc(f.competition)}</div>
      </div>
      <div class="fixture-team away">
        <div class="team-name ${isA ? 'accent' : ''}">${esc(f.awayTeam)}</div>
        ${isA ? '<div class="fixture-label">Away</div>' : ''}
      </div>
    </div>`;
}

/* ════════════════════════════════════════════════════════════
   SQUAD
   ════════════════════════════════════════════════════════════ */

async function renderSquad() {
  const panel = document.getElementById('panel-squad');
  panel.innerHTML = spinner();
  try {
    const players = await api(`/roster/${encodeURIComponent(state.favoriteTeam)}`);
    if (!players.length) { panel.innerHTML = empty('👥', 'No squad data available'); return; }

    const order  = ['Goalkeeper', 'Defender', 'Midfielder', 'Forward'];
    const groups = Object.fromEntries([...order, 'Other'].map(p => [p, []]));
    players.forEach(p => (groups[p.position] ?? groups['Other']).push(p));

    let html = sectionHeader('Squad', `${players.length} players`);
    [...order, 'Other'].forEach(pos => {
      const members = groups[pos];
      if (!members.length) return;
      html += `
        <div class="position-group">
          <div class="position-label">${pos}s</div>
          <div class="squad-grid">${members.map(playerCard).join('')}</div>
        </div>`;
    });

    panel.innerHTML = html;
  } catch {
    panel.innerHTML = empty('⚠️', 'Could not load squad');
  }
}

function playerCard(p) {
  const age = p.dateOfBirth
    ? Math.floor((Date.now() - new Date(p.dateOfBirth)) / 31_557_600_000)
    : null;
  return `
    <div class="player-card">
      <div class="player-number">${p.shirtNumber || '–'}</div>
      <div class="player-name">${esc(p.name)}</div>
      <div class="player-position">${esc(p.position)}</div>
      <div class="player-meta">${esc(p.nationality)}${age ? ` · ${age}y` : ''}</div>
    </div>`;
}

/* ════════════════════════════════════════════════════════════
   STANDINGS
   ════════════════════════════════════════════════════════════ */

async function renderStandings() {
  const panel = document.getElementById('panel-standings');
  panel.innerHTML = spinner();

  if (!state.team) { panel.innerHTML = empty('📊', 'Team info unavailable'); return; }

  const league  = state.team.league;
  const nowYear = new Date().getFullYear();

  try {
    let standings = null;
    for (const season of [nowYear, nowYear - 1, nowYear - 2]) {
      standings = await api(`/standings/${encodeURIComponent(league)}/${season}`).catch(() => null);
      if (standings?.length) break;
    }

    if (!standings?.length) { panel.innerHTML = empty('📊', 'Standings not yet available'); return; }

    const season = standings[0]?.season ?? nowYear;
    panel.innerHTML = `
      ${sectionHeader(esc(league), `${season}/${String(season+1).slice(2)}`)}
      <div class="table-wrap">
        <table class="standings-table">
          <thead><tr>
            <th class="c">#</th>
            <th>Team</th>
            <th class="c">P</th>
            <th class="c">W</th>
            <th class="c hide-mobile">D</th>
            <th class="c hide-mobile">L</th>
            <th class="c hide-mobile">GD</th>
            <th class="c">Pts</th>
          </tr></thead>
          <tbody>${standings.map(standingRow).join('')}</tbody>
        </table>
      </div>`;
  } catch {
    panel.innerHTML = empty('⚠️', 'Could not load standings');
  }
}

function standingRow(s) {
  const me  = s.teamName === state.favoriteTeam;
  const gd  = s.goalDifference > 0 ? `+${s.goalDifference}` : s.goalDifference;
  return `
    <tr class="${me ? 'me' : ''}">
      <td class="c st-rank">${s.rank}</td>
      <td class="st-team">${me ? `<strong>${esc(s.teamName)}</strong>` : esc(s.teamName)}</td>
      <td class="c">${s.played}</td>
      <td class="c">${s.won}</td>
      <td class="c hide-mobile">${s.drawn}</td>
      <td class="c hide-mobile">${s.lost}</td>
      <td class="c hide-mobile">${gd}</td>
      <td class="c st-pts">${s.points}</td>
    </tr>`;
}

/* ════════════════════════════════════════════════════════════
   NOTIFICATIONS
   ════════════════════════════════════════════════════════════ */

async function renderNotifications() {
  const panel = document.getElementById('panel-notifications');
  panel.innerHTML = spinner();
  try {
    const notifs = await api(`/notifications/${state.userId}`);
    syncNotifBadge(notifs);

    if (!notifs.length) { panel.innerHTML = empty('🔔', 'No notifications yet'); return; }

    const unread = notifs.filter(n => !n.isRead).length;
    panel.innerHTML =
      sectionHeader('Notifications', `${unread} unread`) +
      `<div class="notif-list">${notifs.map(notifItem).join('')}</div>`;

    panel.querySelectorAll('.notif-item.unread').forEach(el => {
      el.addEventListener('click', () => markRead(el.dataset.id, el));
    });
  } catch {
    panel.innerHTML = empty('⚠️', 'Could not load notifications');
  }
}

function notifItem(n) {
  return `
    <div class="notif-item ${n.isRead ? 'read' : 'unread'}" data-id="${n.id}">
      <div class="notif-dot"></div>
      <div class="notif-body">
        <div class="notif-title">${esc(n.title)}</div>
        <div class="notif-message">${esc(n.message)}</div>
        <div class="notif-time">${timeAgo(new Date(n.createdAt))}</div>
      </div>
    </div>`;
}

async function markRead(id, el) {
  if (el.classList.contains('read')) return;
  try {
    await api(`/notifications/${id}/read`, { method: 'PUT' });
    el.classList.replace('unread', 'read');
    el.querySelector('.notif-dot').style.background = 'var(--text-dim)';
    // Update badge count
    const unread = document.querySelectorAll('.notif-item.unread').length;
    const badge  = document.getElementById('notif-count');
    if (unread === 0) badge.hidden = true;
    else { badge.textContent = unread > 9 ? '9+' : unread; badge.hidden = false; }
  } catch { /* ignore */ }
}

async function pollNotifCount() {
  try {
    const notifs = await api(`/notifications/${state.userId}`);
    syncNotifBadge(notifs);
  } catch { /* ignore */ }
}

function syncNotifBadge(notifs) {
  const unread = notifs.filter(n => !n.isRead).length;
  const badge  = document.getElementById('notif-count');
  badge.hidden = unread === 0;
  if (unread > 0) badge.textContent = unread > 9 ? '9+' : unread;
}

/* ════════════════════════════════════════════════════════════
   MICRO-HELPERS
   ════════════════════════════════════════════════════════════ */

function spinner() {
  return '<div class="loading"><div class="spinner"></div></div>';
}

function empty(icon, msg) {
  return `<div class="empty-state"><div class="empty-icon">${icon}</div><p>${msg}</p></div>`;
}

function sectionHeader(title, meta) {
  return `<div class="section-header">
    <span class="section-title">${title}</span>
    <span class="section-meta">${meta}</span>
  </div>`;
}

function setLoading(btn, on, label) {
  btn.disabled    = on;
  btn.textContent = label;
}

function showError(el, msg) {
  el.textContent    = msg;
  el.style.display  = 'block';
}

function setImg(id, src, alt) {
  const el = document.getElementById(id);
  if (el) { el.src = src || ''; el.alt = alt || ''; }
}

function setText(id, val) {
  const el = document.getElementById(id);
  if (el) el.textContent = val || '';
}

function esc(str) {
  return String(str ?? '')
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
    .replace(/"/g,'&quot;').replace(/'/g,'&#39;');
}

function timeAgo(date) {
  const s = Math.floor((Date.now() - date) / 1000);
  if (s < 60)          return 'Just now';
  if (s < 3600)        return `${Math.floor(s/60)}m ago`;
  if (s < 86400)       return `${Math.floor(s/3600)}h ago`;
  if (s < 7 * 86400)   return `${Math.floor(s/86400)}d ago`;
  return date.toLocaleDateString('en-GB', { day:'numeric', month:'short' });
}
