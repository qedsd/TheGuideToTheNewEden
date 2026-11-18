# -*- coding: utf-8 -*-
"""给"没能匹配上 SDE 官方译名"的 wiki 页找候选，并区分页面性质。

190 条未命中里其实混着三类东西：
  1. 脏数据 / 用户沙盒 —— `User:xxx/sandbox`、`Storyline mission reports`（不是任务）
  2. 结构页 —— 史诗任务线的 `X/Chapter 1 - …`、`Amarr COSMOS`、`Excalation` 这类枢纽页
  3. 真任务 —— Angel Strike 系列、Booster Manufacturing、Flame of Peace……
第 3 类才需要跟 SDE 对齐；前两类应当从任务表里剔除（它们没有独立的游戏内任务名）。

输出：sde_candidates.json = [{title, kind, cands: [{en, zh, score}]}]
"""

import difflib
import io
import json
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SDE = os.path.join(HERE, 'sde_missions.json')
MISS = os.path.join(HERE, 'sde_missing.json')
OUT = os.path.join(HERE, 'sde_candidates.json')

USER = re.compile(r'^User:')
CHAPTER = re.compile(r'/Chapter\s*\d', re.I)
HUB = {
    'storyline mission reports', 'epic arc navigation list', 'pirate epic arc advice',
    'angel strike (blood raiders)', "angel strike (guristas pirates)", "angel strike (sansha's nation)",
    'guristas strike', 'kidnappers strike', 'kidnappers strike part 1-4',
    'after the seven', 'air show!', 'the missing reporters', 'what\'s in a name',
    'pot and kettle', 'whispers in the dark', 'the kidnapping', 'flame of peace',
    'driving a wedge', 'driving a wedge (alternate variant)',
}
COSMOS = re.compile(r'^(amarr|caldari|gallente|minmatar|angel|blood raider|guristas|serpentis)\s+cosmos$', re.I)


def classify(title):
    t = title.strip()
    if USER.match(t):
        return 'junk'
    if t.lower() in HUB:
        return 'hub'
    if CHAPTER.search(t):
        return 'chapter'
    if COSMOS.match(t):
        return 'hub'
    return 'mission'


def norm(s):
    s = (s or '').lower()
    s = re.sub(r'\([^)]*\)', ' ', s)
    s = re.sub(r'[^0-9a-z\u4e00-\u9fff]+', ' ', s)
    return re.sub(r'\s+', ' ', s).strip()


def main():
    sde = json.load(io.open(SDE, encoding='utf-8'))
    miss = json.load(io.open(MISS, encoding='utf-8'))

    items = []
    for title in miss:
        kind = classify(title)
        key = norm(title)
        scored = []
        for k, v in sde.items():
            score = difflib.SequenceMatcher(None, key, norm(v['en'])).ratio()
            if score >= 0.72:
                scored.append({'en': v['en'], 'zh': v['zh'], 'score': round(score, 3)})
        scored.sort(key=lambda x: -x['score'])
        items.append({'title': title, 'kind': kind, 'cands': scored[:3]})

    json.dump(items, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    by_kind = {}
    for i in items:
        by_kind[i['kind']] = by_kind.get(i['kind'], 0) + 1
    print('分类：', by_kind)

    for kind in ('mission',):
        print('\n=== 待人工/模型确认的真任务（%d）===' % by_kind.get(kind, 0))
        for i in items:
            if i['kind'] != kind:
                continue
            top = i['cands'][0] if i['cands'] else None
            line = '%-52s' % i['title'][:52]
            if top and top['score'] >= 0.9:
                line += ' ≈ %-40s %s  (%.2f)' % (top['en'][:40], top['zh'], top['score'])
            elif top:
                line += ' ? %-40s %s  (%.2f)' % (top['en'][:40], top['zh'], top['score'])
            else:
                line += ' (无候选)'
            print(line)
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
