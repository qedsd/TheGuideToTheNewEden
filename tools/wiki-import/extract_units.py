"""抽取需要翻译成中文的英文片段，按"波次"分批，供翻译代理逐批处理。

为什么要分波
------------
全量 380 条站点、近 500KB 英文正文，一次性翻完既慢又难校验。按页面价值分三波：

- **wave1**：DED 评级复合体 + 无评级复合体 + 远征（页面主体，玩家最常查）
- **wave2**：战斗异常（126 条，含"可升级为"映射）
- **wave3**：化学实验室 / 战斗遗迹 / 三神裔 / 特殊站点

每波独立产出 `zh_w{N}_*.json`，`build_sites.py` 允许部分回填——没翻到的片段按原文保留，
下一波补齐即可，不会出现"翻一半就整体不可用"。

产物
----
- `units.json`      —— { id: {text, kind, wave} }
- `batch_w{N}_XX.json` —— { id: 英文 }，每批约 15KB
- `names_w{N}.json` —— { 站点英文名: 页面 }，供站点名翻译
"""

import hashlib
import io
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
MAX_BATCH_CHARS = 26000
MAX_BATCH_UNITS = 200
MAX_MESSAGE_CHARS = 200

SKIP_SECTION_TITLES = {'see also', 'references', "music ('cause why not)"}
SKIP_MESSAGE_TITLES = {'database information', 'database information '}

WAVE_OF_BUCKET = {
    'dedRated': 1,
    'unrated': 1,
    'expeditions': 1,
    'anomalies': 2,
    'chemicalLabs': 3,
    'relicSites': 3,
    'triglavian': 3,
    'special': 3,
}


def uid(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:12]


def page_categories():
    """wiki 页名 -> 所属波次（同一页被多个桶引用时取最小波次）。"""
    idx = json.load(io.open(os.path.join(HERE, 'index.json'), encoding='utf-8'))
    waves = {}
    for bucket, items in idx.items():
        if not isinstance(items, list):
            continue
        w = WAVE_OF_BUCKET.get(bucket, 3)
        for it in items:
            page = it.get('page')
            if page:
                waves[page] = min(wave for wave in (waves.get(page, 9), w))
    return waves


def iter_units(v):
    """遍历单页里所有需要翻译的英文片段：yield (kind, text)。"""
    for s in v['intro']:
        if s:
            yield 'intro', s

    for pocket in v['pockets']:
        if pocket['title']:
            yield 'title', pocket['title']
        if pocket['prose']:
            yield 'prose', pocket['prose']
        for block in pocket['blocks']:
            if block.get('title'):
                yield 'title', block['title']
            for row in block['rows']:
                if row.get('separator') is not None:
                    if row.get('separator'):
                        yield 'title', row['separator']
                    continue
                for field in ('trigger', 'note', 'cargo'):
                    t = (row.get(field) or '').strip()
                    if t:
                        yield 'row', t

    for sec in v['notes']:
        title, s = sec['title'], sec['text']
        if title:
            yield 'title', title
        if s and title.strip().lower() not in SKIP_SECTION_TITLES:
            yield 'note', s
    for sec in v['rewards']:
        title, s = sec['title'], sec['text']
        if title:
            yield 'title', title
        if s:
            yield 'reward', s
    for sec in v['escalation']:
        title, s = sec['title'], sec['text']
        if title:
            yield 'title', title
        if s:
            yield 'escalation', s

    for m in v['messages']:
        title = (m[1] if len(m) > 1 else '').strip()
        if title.lower() in SKIP_MESSAGE_TITLES:
            continue
        if title:
            yield 'title', title
        body = (' '.join(m[2:]) if len(m) > 2 else '').strip()
        if body and len(body) <= MAX_MESSAGE_CHARS:
            yield 'message', body

    # 战利品表：物品名 / 来源 / 备注（估值是数字/ISK，不用翻）
    for row in v.get('lootRows') or []:
        for i in (0, 2, 3):
            if i < len(row):
                cell = (row[i] or '').strip()
                if cell and len(cell) <= 220:
                    yield 'loot', cell


def main():
    parsed = json.load(io.open(os.path.join(HERE, 'parsed.json'), encoding='utf-8'))
    waves = page_categories()

    units = {}
    for page, v in parsed.items():
        wave = waves.get(page, 3)
        for kind, text in iter_units(v):
            key = uid(text)
            if key not in units:
                units[key] = {'text': text, 'kind': kind, 'wave': wave}
            else:
                units[key]['wave'] = min(units[key]['wave'], wave)

    json.dump(units, io.open(os.path.join(HERE, 'units.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    for w in (1, 2, 3):
        sub = {k: u for k, u in units.items() if u['wave'] == w}
        chars = sum(len(u['text']) for u in sub.values())
        print('wave%d：%d 条 / %d 字符' % (w, len(sub), chars))

    # 站点名（只保留有页面的）
    names = {}
    for page, v in parsed.items():
        n = (v['name'] or '').strip()
        if n:
            names.setdefault(n, waves.get(page, 3))
    for w in (1, 2, 3):
        sub = {k: v for k, v in names.items() if v == w}
        print('wave%d 站点名：%d 个' % (w, len(sub)))

    # 清掉旧批次
    for f in os.listdir(HERE):
        if (f.startswith('batch_w') or f.startswith('names_w')) and f.endswith('.json'):
            os.remove(os.path.join(HERE, f))

    for w in (1, 2, 3):
        sub = [(k, u) for k, u in units.items() if u['wave'] == w]
        batches, cur, cur_chars = [], [], 0
        for key, u in sub:
            n = len(u['text'])
            if cur and (cur_chars + n > MAX_BATCH_CHARS or len(cur) >= MAX_BATCH_UNITS):
                batches.append(cur)
                cur, cur_chars = [], 0
            cur.append(key)
            cur_chars += n
        if cur:
            batches.append(cur)
        for i, keys in enumerate(batches, 1):
            json.dump({k: units[k]['text'] for k in keys},
                      io.open(os.path.join(HERE, 'batch_w%d_%02d.json' % (w, i)), 'w', encoding='utf-8'),
                      ensure_ascii=False, indent=1)
        print('wave%d：%d 批' % (w, len(batches)))

        nl = [k for k, v in names.items() if v == w]
        json.dump({k: True for k in nl},
                  io.open(os.path.join(HERE, 'names_w%d.json' % w), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()



