// ---------- Full multi-wave cutscene loop mock ----------

const els = {
  stage: document.getElementById('stage'),
  phase: document.getElementById('phase-tag'),
  hero: document.getElementById('actor-hero'),
  boss: document.getElementById('actor-boss'),
  portrait: document.getElementById('portrait'),
  portraitCamTheseus: document.querySelector('.portrait__cam--theseus'),
  portraitCamMinotaur: document.querySelector('.portrait__cam--minotaur'),
  dialogue: document.getElementById('dialogue'),
  nameplateText: document.getElementById('nameplate-text'),
  text: document.getElementById('dialogue-text'),
  fade: document.getElementById('fade'),
  tierCard: document.getElementById('tier-card'),
  tierLabel: document.getElementById('tier-label'),
  tierDebuff: document.getElementById('tier-debuff'),
  ending: document.getElementById('ending'),
  endingReplay: document.getElementById('ending-replay'),
  hud: document.getElementById('hud'),
  bossHp: document.getElementById('bossHp'),
  heroHp: document.getElementById('heroHp'),
  controls: document.getElementById('controls'),
  replayBtn: document.getElementById('replay-btn'),
  killHeroBtn: document.getElementById('kill-hero-btn'),
  killBossBtn: document.getElementById('kill-boss-btn'),
  sfxToggle: document.getElementById('sfx-toggle'),
  walkSpeed: document.getElementById('walk-speed'),
  speed: document.getElementById('speed-range'),
};

const SPEAKER_DISPLAY = { theseus: 'Theseus', minotaur: 'Minotaur' };
const FAST_FORWARD_MULT = 8; // Hold space → 8x speed
const LINE_HOLD_MS = 700;    // pause between fully-typed lines

// ---------- Audio ----------
const audio = (() => {
  let ctx = null;
  const ensure = () => { if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)(); if (ctx.state === 'suspended') ctx.resume(); return ctx; };
  return {
    tick(speaker) {
      if (!els.sfxToggle.checked) return;
      const ac = ensure();
      const now = ac.currentTime;
      const osc = ac.createOscillator(); const gain = ac.createGain();
      osc.type = 'square';
      osc.frequency.value = (speaker === 'minotaur' ? 130 : 220) + (Math.random() * 2 - 1) * 16;
      gain.gain.value = 0;
      gain.gain.linearRampToValueAtTime(0.05, now + 0.005);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.05);
      osc.connect(gain).connect(ac.destination);
      osc.start(now); osc.stop(now + 0.06);
    },
    boom() {
      const ac = ensure(); const now = ac.currentTime;
      const osc = ac.createOscillator(); const gain = ac.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(140, now);
      osc.frequency.exponentialRampToValueAtTime(40, now + 0.25);
      gain.gain.value = 0;
      gain.gain.linearRampToValueAtTime(0.18, now + 0.01);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.4);
      osc.connect(gain).connect(ac.destination);
      osc.start(now); osc.stop(now + 0.45);
    },
    deathRing() {
      const ac = ensure(); const now = ac.currentTime;
      const osc = ac.createOscillator(); const gain = ac.createGain();
      osc.type = 'sawtooth';
      osc.frequency.setValueAtTime(220, now);
      osc.frequency.exponentialRampToValueAtTime(60, now + 0.6);
      gain.gain.value = 0;
      gain.gain.linearRampToValueAtTime(0.12, now + 0.01);
      gain.gain.exponentialRampToValueAtTime(0.0001, now + 0.7);
      osc.connect(gain).connect(ac.destination);
      osc.start(now); osc.stop(now + 0.75);
    }
  };
})();

// ---------- Phase log ----------
function setPhase(name) { els.phase.textContent = `Phase: ${name}`; }
const wait = (ms) => new Promise(r => setTimeout(r, ms));

// ---------- Global input state ----------
let fastForward = false;
document.addEventListener('keydown', e => {
  if (e.code === 'Space') { e.preventDefault(); fastForward = true; }
});
document.addEventListener('keyup', e => {
  if (e.code === 'Space') fastForward = false;
});

// ---------- Actors ----------
function resetActors() {
  els.hero.classList.remove('is-walking', 'is-arrived', 'is-dead');
}

function walkInHero() {
  return new Promise(resolve => {
    const dur = parseInt(els.walkSpeed.value, 10);
    document.documentElement.style.setProperty('--walk-duration', `${dur}ms`);
    els.hero.classList.add('is-walking');
    requestAnimationFrame(() => els.hero.classList.add('is-arrived'));
    const onEnd = () => {
      els.hero.classList.remove('is-walking');
      els.hero.removeEventListener('transitionend', onEnd);
      audio.boom();
      resolve();
    };
    els.hero.addEventListener('transitionend', onEnd);
  });
}

function killHero() {
  els.hero.classList.add('is-dead');
  audio.deathRing();
}

// ---------- Dialogue (auto-advance, fast-forward on Space) ----------
function setActiveSpeaker(speaker) {
  els.portraitCamTheseus.classList.toggle('is-active', speaker === 'theseus');
  els.portraitCamMinotaur.classList.toggle('is-active', speaker === 'minotaur');
  els.nameplateText.textContent = SPEAKER_DISPLAY[speaker] || '';
  els.dialogue.classList.toggle('is-hero', speaker === 'theseus');
}

async function typeLine(line) {
  setActiveSpeaker(line.speaker);
  els.text.textContent = '';
  const baseInterval = parseInt(els.speed.value, 10);
  for (let i = 0; i < line.text.length; i++) {
    const ch = line.text.charAt(i);
    els.text.textContent += ch;
    if (ch !== ' ' && ch !== '\n') audio.tick(line.speaker);
    const interval = fastForward ? baseInterval / FAST_FORWARD_MULT : baseInterval;
    await wait(interval);
  }
}

async function playScript(lines) {
  els.dialogue.hidden = false;
  els.portrait.hidden = false;
  for (let i = 0; i < lines.length; i++) {
    await typeLine(lines[i]);
    // Auto-advance: wait a beat (faster if fast-forward held)
    const hold = fastForward ? LINE_HOLD_MS / FAST_FORWARD_MULT : LINE_HOLD_MS;
    await wait(hold);
  }
  els.dialogue.hidden = true;
  els.portrait.hidden = true;
}

// ---------- Mock combat ----------
async function runCombat() {
  els.hud.hidden = false;
  let boss = 100, hero = 100;
  els.bossHp.style.width = '100%'; els.heroHp.style.width = '100%';
  while (boss > 0 && hero > 0 && !killHeroFlag && !killBossFlag) {
    await wait(50);
    boss -= Math.random() * 3;
    hero -= Math.random() * 4;
    els.bossHp.style.width = `${Math.max(0, boss)}%`;
    els.heroHp.style.width = `${Math.max(0, hero)}%`;
  }
  els.hud.hidden = true;
  if (killHeroFlag || hero <= 0) return 'hero';
  if (killBossFlag || boss <= 0) return 'boss';
  return hero <= 0 ? 'hero' : 'boss';
}

let killHeroFlag = false;
let killBossFlag = false;
els.killHeroBtn.addEventListener('click', () => { killHeroFlag = true; });
els.killBossBtn.addEventListener('click', () => { killBossFlag = true; });

// ---------- Fade + tier card ----------
async function fadeIn(durMs = 600)  { els.fade.classList.add('is-on'); await wait(durMs); }
async function fadeOut(durMs = 600) { els.fade.classList.remove('is-on'); await wait(durMs); }
async function showTierCard(tier) {
  els.tierLabel.textContent = tier.label;
  els.tierDebuff.textContent = tier.debuff;
  els.tierCard.hidden = false;
  requestAnimationFrame(() => els.tierCard.classList.add('is-visible'));
  await wait(1800);
  els.tierCard.classList.remove('is-visible');
  await wait(380);
  els.tierCard.hidden = true;
}

// ---------- The full loop ----------
async function runLoop() {
  // Reset everything
  killHeroFlag = false; killBossFlag = false;
  resetActors();
  els.stage.classList.remove('is-cinematic');
  els.dialogue.hidden = true; els.portrait.hidden = true;
  els.fade.classList.remove('is-on');
  els.tierCard.hidden = true; els.ending.hidden = true; els.hud.hidden = true;

  const data = await fetch('dialogue.json').then(r => r.json());

  const waves = [
    { intro: 'intro_wave_1', death: 'hero_death_wave_1' },
    { intro: 'intro_wave_2', death: 'hero_death_wave_2' },
    { intro: 'intro_wave_3', death: 'hero_death_wave_3' },
  ];

  for (let w = 0; w < waves.length; w++) {
    const wave = waves[w];

    setPhase(`wave ${w + 1}: cutscene-intro (letterbox + walk-in)`);
    els.stage.classList.add('is-cinematic');
    await wait(450);
    resetActors();   // ensure hero is offstage left for the new wave
    await wait(50);
    await walkInHero();
    await wait(200);

    setPhase(`wave ${w + 1}: pre-fight dialogue`);
    await playScript(data[wave.intro]);

    setPhase(`wave ${w + 1}: combat`);
    els.stage.classList.remove('is-cinematic');
    const outcome = await runCombat();

    if (outcome === 'boss') {
      // Boss died → game over branch
      setPhase('game-over: dialogue');
      els.stage.classList.add('is-cinematic');
      await wait(300);
      await playScript(data.game_over);
      await fadeIn(800);
      els.ending.hidden = false;
      setPhase('victory');
      return;
    }

    // Hero died → quick cutscene + tier card
    setPhase(`wave ${w + 1}: hero-death cutscene`);
    killHero();
    await wait(400);
    els.stage.classList.add('is-cinematic');
    await wait(350);
    await playScript(data[wave.death]);
    setPhase(`wave ${w + 1}: fade to tier card`);
    await fadeIn(600);
    if (w < waves.length - 1) {
      await showTierCard(data.tiers[w]);
    } else {
      // Last wave: shouldn't happen if outcome was 'hero' but just in case
      els.ending.hidden = false;
      els.fade.classList.remove('is-on');
      return;
    }
    // Fade back out (next wave intro begins under cinematic letterbox)
    await fadeOut(450);
  }

  // Shouldn't normally hit here — but if we run out of waves with the hero still dying:
  setPhase('ran out of waves — boss wins by default');
  els.ending.hidden = false;
}

// ---------- Wiring ----------
els.replayBtn.addEventListener('click', () => runLoop());
els.endingReplay.addEventListener('click', () => runLoop());
els.controls.addEventListener('click', e => e.stopPropagation());

// Auto-start
runLoop();
