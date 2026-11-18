# -*- coding: utf-8 -*-
"""导出**剩余**待翻译片段并切批（属性字段 / NPC 备注 / 正文散文 / wiki 简报）。

与第一轮 `export_translations.py` 的区别：那一轮只导了短字段；这一轮把
连载性质的正文段落和 wiki 英文简报也纳入，用于"把剩下的全译完"。

产出：
  to_translate2/batch_XXX.json   [{"k": "<sha1[:16]>", "en": "<英文>"}]
  to_translate2/manifest.json    {batch: n, chars: n, groups: {...}}

译文按键回填：`import_translations2.py` 读 `to_translate2/zh_XXX.json`
（{"<k>": "<中文>"}）合并进 translations.json。
"""

import hashlib
import io
import json
import os
import re
import sqlite3

HERE = os.path.dirname(os.path.abspath(__file__))
DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
OUT_DIR = os.path.join(HERE, 'to_translate2')
BATCH_CHARS = 9000          # 每批的英文字符上限（约 120–200 条短句 / 20–40 段长文）

CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
TABLE_ROW = re.compile(r'^\|')


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def collect():
    trans = json.load(io.open(os.path.join(HERE, 'translations.json'), encoding='utf-8'))
    parsed = json.load(io.open(os.path.join(HERE, 'parsed.json'), encoding='utf-8'))

    groups = {'field': {}, 'note': {}, 'prose': {}, 'brief': {}}

    # 1) 属性字段 / 备注 / 正文：直接扫库里的成品正文（已经清理干净、格式统一）
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute("select Objective, Ewar, ShipLimit, ShipSuggestion, Rewards, Extra, "
                "StandingLoss, WikiContent from Mission")
    for obj, ewar, slim, ssug, rew, extra, stand, body in cur.fetchall():
        for v in (obj, ewar, slim, ssug, rew, extra, stand):
            v = (v or '').strip()
            if v and not CJK.search(v) and key(v) not in trans:
                groups['field'][v] = True

        for m in re.finditer(r'^\| ([^|]+) \| ([\d\-~?]+) \| ([^|]*) \| ([^|]*) \|$', body or '', re.M):
            note = m.group(4).strip()
            if note and not CJK.search(note) and key(note) not in trans:
                groups['note'][note] = True

        # 注意：不要用"进入简报段就跳过后续行"的写法——简报后面常常直接跟正文且没有标题，
        # 会把整段正文误判成简报而永远不送翻（第一轮就吃了这个亏，漏了 2 万多行）
        for line in (body or '').split('\n'):
            s = line.strip()
            if not s or TABLE_ROW.match(s):
                continue
            if s.startswith('### ') or s.startswith('**') or s.startswith('> '):
                continue
            if CJK.search(s) or len(LATIN.findall(s)) < 2:
                continue
            if key(s) not in trans:
                groups['prose'][s] = True

    # 2) wiki 简报：parsed.json 里没存，从 raw 重新抽（只取 SDE 没给中文简报的页）
    import parse_missions as P
    for page in parsed:
        if page.get('excluded') or page.get('sdeBriefing'):
            continue
        path = os.path.join(HERE, 'raw', page.get('file') or '')
        if not page.get('file') or not os.path.exists(path):
            continue
        text = io.open(path, encoding='utf-8').read()
        for _s, _e, _n, body in P.find_templates(text, {'MissionBriefing'}):
            cleaned = P.paragraphize(body)
            for para in re.split(r'\n{2,}', cleaned):
                p = para.strip()
                if not p or CJK.search(p) or len(LATIN.findall(p)) < 2:
                    continue
                if p.startswith('### ') or p.startswith('**') or TABLE_ROW.match(p):
                    continue
                if key(p) not in trans:
                    groups['brief'][p] = True
    return groups


def main():
    groups = collect()
    os.makedirs(OUT_DIR, exist_ok=True)
    for old in os.listdir(OUT_DIR):
        if old.endswith('.json'):
            os.remove(os.path.join(OUT_DIR, old))

    entries = []
    for name in ('field', 'note', 'brief', 'prose'):
        for text in sorted(groups[name]):
            entries.append((name, text))

    batches, cur_batch, cur_chars = [], [], 0
    for name, text in entries:
        if cur_batch and cur_chars + len(text) > BATCH_CHARS:
            batches.append(cur_batch)
            cur_batch, cur_chars = [], 0
        cur_batch.append({'k': key(text), 'en': text, 'g': name})
        cur_chars += len(text)
    if cur_batch:
        batches.append(cur_batch)

    counts = {}
    for idx, batch in enumerate(batches):
        path = os.path.join(OUT_DIR, 'batch_%03d.json' % idx)
        json.dump(batch, io.open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        for item in batch:
            counts[item['g']] = counts.get(item['g'], 0) + 1

    manifest = {
        'batches': len(batches),
        'items': len(entries),
        'chars': sum(len(t) for _n, t in entries),
        'groups': counts,
    }
    json.dump(manifest, io.open(os.path.join(OUT_DIR, 'manifest.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('批次 %d 个，片段 %d 条，%d 字符' % (manifest['batches'], manifest['items'], manifest['chars']))
    print('分布：', counts)


if __name__ == '__main__':
    main()
