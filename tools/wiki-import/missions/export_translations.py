# -*- coding: utf-8 -*-
"""把需要翻译的短文本导成批次文件，交给翻译（人或模型），再 merge_translations.py 合并。

只导出**短文本**：任务名、小节标题、infobox 里的字段值。
长正文（walkthrough 段落，合计约 100 万字符）**不导出**——量太大，且结构字段已经能支撑查表；
正文按需在后续批次里补（同一套 key 机制，补完重跑 build 即可）。

产物：
  to_translate/batch_XX.json  —— [[key, 英文], …]
  to_translate/meta.json      —— {"count": N, "chars": M}
key = sha1(英文片段)[:16]，与 build_missions.py 的 tr() 完全一致。
"""

import hashlib
import io
import json
import os
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PARSED = os.path.join(HERE, 'parsed.json')
OUT_DIR = os.path.join(HERE, 'to_translate')
BATCH_CHARS = 12000

STATIC_DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
             r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")

FIELD_KEYS = ['Objective', 'WebPoint', 'EWAR', 'ShipSizeLimit', 'ShipSuggestion',
              'Rewards', 'StandingLoss', 'Extra', 'Type']

# 本地词典已经覆盖的短值，不必再导（与 build_missions.py 的 VALUE_CN/TYPE_CN 保持一致）
SKIP_VALUES = {
    'frigate', 'destroyer', 'cruiser', 'battlecruiser', 'battleship', 'industrial',
    'hauler', 'freighter', 'mining barge', 'exhumer', 'none', 'any', 'encounter',
    'courier', 'mining', 'trade', 'storyline', 'web', 'scrambler', 'neut', 'ecm',
}


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def legacy_titles():
    """老 Mission 表里的中文任务名（405 条）直接用，免得重复翻译。"""
    out = {}
    try:
        con = sqlite3.connect('file:%s?mode=ro' % STATIC_DB, uri=True)
        for en, zh in con.execute('select Title_En, Title_Zh from Mission'):
            if en and zh:
                out[en.strip()] = zh.strip()
        con.close()
    except Exception as ex:  # noqa: BLE001
        print('  读老表失败（忽略）：%s' % ex)
    return out


def main():
    pages = json.load(io.open(PARSED, encoding='utf-8'))
    tm = legacy_titles()
    print('老表中文名可用：%d 条' % len(tm))

    seen = {}
    reused = 0

    def add(text):
        nonlocal reused
        t = (text or '').strip()
        if not t or len(t) < 2:
            return
        k = key(t)
        if k in seen:
            return
        seen[k] = t

    for pg in pages:
        title = pg['title']
        if title in tm:
            reused += 1
        else:
            add(title)
        for v in pg['variants']:
            for s in v.get('sections') or []:
                add(s.get('title') or '')
                for tb in s.get('npcs') or []:
                    add(tb.get('title') or '')
            info = v.get('info') or {}
            for f in FIELD_KEYS:
                val = str(info.get(f, '') or '').strip()
                if not val or val.lower() in SKIP_VALUES:
                    continue
                add(val)

    # 老表里已有的名字先写进译文，避免重复翻译
    import build_missions as B
    trans = {}
    for en, zh in tm.items():
        trans[key(en)] = zh

    os.makedirs(OUT_DIR, exist_ok=True)
    items = sorted(seen.items(), key=lambda kv: -len(kv[1]))
    batches, cur, cur_chars = [], [], 0
    for k, text in items:
        if cur and cur_chars + len(text) > BATCH_CHARS:
            batches.append(cur)
            cur, cur_chars = [], 0
        cur.append([k, text])
        cur_chars += len(text)
    if cur:
        batches.append(cur)

    for i, b in enumerate(batches):
        json.dump(b, io.open(os.path.join(OUT_DIR, 'batch_%02d.json' % i), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)

    meta = {
        'count': len(items),
        'chars': sum(len(t) for _k, t in items),
        'batches': len(batches),
        'legacyReused': reused,
    }
    json.dump(meta, io.open(os.path.join(OUT_DIR, 'meta.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    # 老表中文名先落一份译文，后续批次再追加
    json.dump(trans, io.open(os.path.join(HERE, 'translations.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    print('待翻译 %d 条片段 / %d 字符，切成 %d 批（每批约 %d 字符）'
          % (meta['count'], meta['chars'], meta['batches'], BATCH_CHARS))
    for i, b in enumerate(batches):
        print('  batch_%02d: %d 条 / %d 字符' % (i, len(b), sum(len(t) for _k, t in b)))
    return 0


if __name__ == '__main__':
    sys.exit(main())
