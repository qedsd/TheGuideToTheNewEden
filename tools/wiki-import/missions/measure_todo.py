# -*- coding: utf-8 -*-
"""统计"待翻译"真实体量（去重后），决定批次划分。

四类：属性字段片段、NPC 备注整句、wiki 英文简报（仅 SDE 没覆盖的任务）、正文散文段落。
"""

import hashlib
import io
import json
import os
import re
import sqlite3

DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
HERE = r"D:\QEDSD\Code\TheGuideToTheNewEden\Tools\wiki-import\missions"
CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
TABLE_ROW = re.compile(r'^\|')


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def main():
    trans = json.load(io.open(os.path.join(HERE, 'translations.json'), encoding='utf-8'))
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute("select TitleEN, Objective, Ewar, ShipLimit, ShipSuggestion, Rewards, Extra, WikiContent from Mission")
    rows = cur.fetchall()

    fields, notes, prose = {}, {}, {}
    for _en, obj, ewar, slim, ssug, rew, extra, body in rows:
        for v in (obj, ewar, slim, ssug, rew, extra):
            v = (v or '').strip()
            if v and not CJK.search(v) and key(v) not in trans:
                fields[v] = fields.get(v, 0) + 1
        for m in re.finditer(r'^\| ([^|]+) \| ([\d\-~?]+) \| ([^|]*) \| ([^|]*) \|$', body or '', re.M):
            note = m.group(4).strip()
            if note and not CJK.search(note) and key(note) not in trans:
                notes[note] = notes.get(note, 0) + 1
        in_brief = False
        for line in (body or '').split('\n'):
            s = line.strip()
            if s.startswith('### 简报'):
                in_brief = True
                continue
            if s.startswith('### ') or s.startswith('**') or s.startswith('> '):
                in_brief = False
                continue
            if not s or TABLE_ROW.match(s) or in_brief:
                continue
            if CJK.search(s) or len(LATIN.findall(s)) < 2:
                continue
            if key(s) not in trans:
                prose[s] = prose.get(s, 0) + 1

    def report(name, d):
        chars = sum(len(k) for k in d)
        print('%-10s %5d 条 / %7d 字符' % (name, len(d), chars))
        return chars

    print('待翻译体量（已去重、已排除译文缓存里有的）：')
    total = report('属性字段', fields) + report('NPC备注', notes) + report('正文段落', prose)

    # 简报：只统计 SDE 没给中文简报的任务
    parsed = json.load(io.open(os.path.join(HERE, 'parsed.json'), encoding='utf-8'))
    brief = {}
    for page in parsed:
        if page.get('sdeBriefing') or page.get('excluded'):
            continue
        raw = page.get('briefing')
        if not raw:
            continue
        for para in re.split(r'\n{2,}', raw):
            p = para.strip()
            if p and not CJK.search(p) and key(p) not in trans:
                brief[p] = brief.get(p, 0) + 1
    total += report('简报段落', brief)
    print('合计 %d 字符（约 %d 个 12k 字符批次）' % (total, total // 12000 + 1))

    out = {'fields': fields, 'notes': notes, 'prose': prose, 'brief': brief}
    json.dump({k: sorted(v) for k, v in out.items()},
              io.open(os.path.join(HERE, 'todo_inventory.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)


if __name__ == '__main__':
    main()
