'use strict';

/* View-only markup: app.js continues to own typing, timers, and navigation. */
globalThis.RequiemStoryView = (() => {
  const escape = value => String(value ?? '').replace(/[&<>"']/g, character => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[character]));
  const chapters = {
    prologue: { caption: 'PROLOGUE · 마지막 불씨', location: '영야의 폐허' },
    firstMeal: { caption: 'CHAPTER 01 · 첫 식사', location: '등대 요새' },
    epilogue: { caption: 'CHAPTER 01 · 남겨진 기억', location: '등대 요새' }
  };
  const portraitSpeakers = {
    lian: '리안', sera: '세라', minato: '미나토', heidel: '하이델',
    noa: '노아', kai: '카이', orphe: '오르페', rowan: '로웬', ellen: '엘렌', eve: '이브'
  };

  function listeningPortrait(html) {
    return html.replace(/<img\b[^>]*>/gi, tag => tag.replace(/(\sclass\s*=\s*)(["'])(.*?)\2/i,
      (attribute, prefix, quote, classes) => {
        const names = classes.split(/\s+/);
        if (!names.includes('story-hero') || names.includes('is-listening')) return attribute;
        return `${prefix}${quote}${classes} is-listening${quote}`;
      }));
  }

  function render({ line = {}, key = '', index = 0, total = 0, auto = false,
    portraitHtml = '', listenerHtml = '', footerHtml = '', location = '', caption = '' } = {}) {
    const chapter = chapters[key] || { caption: 'REQUIEM LANTERN · 기억', location: '영야의 폐허' };
    const place = location || (line.background === 'keep' ? '등대 요새' : chapter.location);
    const speaker = String(line.speaker || '').trim();
    const narration = !speaker;
    const portraitId = String(line.portrait || '').trim().toLowerCase();
    const listening = narration || (portraitId && speaker !== portraitSpeakers[portraitId] && speaker.toLowerCase() !== portraitId);
    const portrait = listening ? listeningPortrait(portraitHtml) : portraitHtml;
    const count = total > 0 ? `${String(index + 1).padStart(2, '0')} / ${String(total).padStart(2, '0')}` : '';
    // portraitHtml and footerHtml are trusted markup produced by the existing app helpers.
    return `${line.background === 'black' ? '<div class="blackout"></div>' : ''}
      ${listenerHtml}
      ${portrait}
      <div class="story-top">
        <div class="story-location">
          <span class="story-caption">${escape(caption || chapter.caption)}</span>
          <strong>${escape(place)}</strong>
        </div>
        <div class="story-controls controls" role="group" aria-label="이야기 재생">
          <button type="button" data-action="storylog">기록</button>
          <button type="button" data-action="storyauto">${auto ? '자동 켜짐' : '자동'}</button>
          <button type="button" data-action="storyskip">건너뛰기</button>
        </div>
      </div>
      <button type="button" class="dialogue${narration ? ' is-narration' : ''}" data-action="storynext" aria-label="대화 계속">
        <span class="dialogue-ornament" aria-hidden="true"><i></i></span>
        <span class="speaker">${escape(line.speaker || '')}</span>
        <span class="words"></span>
        <span class="next"><span class="story-page">${escape(count)}</span>다음<i aria-hidden="true"></i></span>
      </button>
      ${footerHtml}`;
  }

  return Object.freeze({ render });
})();
