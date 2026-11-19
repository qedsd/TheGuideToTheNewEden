# -*- coding: utf-8 -*-
"""翻译质量自动修正（第二批：漏译的编号步骤 / 表头）。

第一批（`qa_fix.py`）做的是纯文本替换；这批处理**漏译的整句**：
Wiki 的 Sleeper Cache 走法用 `#` 编号步骤，之前的"整段英文"统计把 `#` 开头的行当成标题跳过了，
所以这些步骤一直没进翻译队列。

用法：
    python qa_fix2.py --export     # 导出待译句子到 to_translate7/
    python qa_fix2.py --apply      # 合并 to_translate7/zh_*.json 并回写
"""

import glob
import hashlib
import io
import json
import os
import re
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.abspath(os.path.join(HERE, '..', '..', 'TheGuideToTheNewEden',
                                  'TheGuideToTheNewEden.WPF', 'Resources', 'Database', 'static.db'))
CACHE = os.path.join(HERE, 'translations_qa.json')
BATCH_DIR = os.path.join(HERE, 'to_translate7')
CJK = re.compile(r'[\u4e00-\u9fff]')
WORD = re.compile(r"[A-Za-z][A-Za-z'\-]*")
MARKER = re.compile(r'^(#{1,3}|[-*]{1,2}|\|)\s*')

TABLES = (('Mission', 'WikiContent'), ('CombatSite', 'WikiContent'),
          ('IncursionSite', 'Content'))


def key(t):
    return hashlib.sha1(t.encode('utf-8')).hexdigest()[:16]


def strip_marker(line):
    s = line.strip()
    m = MARKER.match(s)
    return (s[:m.end()], s[m.end():].strip()) if m else ('', s)


def candidates(only_headers=False):
    con = sqlite3.connect(DB)
    out = set()
    for table, col in TABLES:
        for (body,) in con.execute('select %s from %s' % (col, table)):
            if not body:
                continue
            lines = body.split('\n')
            for i, line in enumerate(lines):
                s = line.strip()
                if not s or CJK.search(s):
                    continue
                marker, text = strip_marker(s)
                if not text or CJK.search(text):
                    continue
                if marker.startswith('|'):
                    # 表头：下一行是分隔行才算
                    if i + 1 < len(lines) and lines[i + 1].strip().startswith('|---'):
                        for cell in text.split('|'):
                            c = cell.strip()
                            if c and not CJK.search(c) and WORD.search(c) and len(WORD.findall(c)) >= 1:
                                out.add(c)
                    continue
                words = WORD.findall(text)
                if len(words) < 6:
                    continue
                if re.search(r'\b(I{1,3}|IV|V|VI{0,3}|IX|X)\b', text):
                    continue
                if re.search(r'\b(Insignia|Implant|Blueprint|BPC|Module|Charge|Turret)\b', text):
                    continue
                out.add(text)
    return sorted(out)


def export():
    cache = json.load(io.open(CACHE, encoding='utf-8')) if os.path.exists(CACHE) else {}
    items = [t for t in candidates() if key(t) not in cache]
    os.makedirs(BATCH_DIR, exist_ok=True)
    for old in os.listdir(BATCH_DIR):
        if old.startswith('batch_') or old.startswith('zh_'):
            os.remove(os.path.join(BATCH_DIR, old))
    batch, size, idx = [], 0, 0
    for text in items:
        if batch and size + len(text) > 8000:
            json.dump([{'k': key(t), 'en': t} for t in batch],
                      io.open(os.path.join(BATCH_DIR, 'batch_%03d.json' % idx), 'w', encoding='utf-8'),
                      ensure_ascii=False, indent=1)
            idx += 1
            batch, size = [], 0
        batch.append(text)
        size += len(text)
    if batch:
        json.dump([{'k': key(t), 'en': t} for t in batch],
                  io.open(os.path.join(BATCH_DIR, 'batch_%03d.json' % idx), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)
        idx += 1
    print('待译 %d 条 / %d 字符 → %d 批' % (len(items), sum(len(i) for i in items), idx))


def apply():
    cache = json.load(io.open(CACHE, encoding='utf-8')) if os.path.exists(CACHE) else {}
    added = 0
    for zh in sorted(glob.glob(os.path.join(BATCH_DIR, 'zh_*.json'))):
        for k, v in json.load(io.open(zh, encoding='utf-8')).items():
            v = (v or '').strip()
            if v and CJK.search(v):
                if k not in cache:
                    added += 1
                cache[k] = v
    json.dump(cache, io.open(CACHE, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文缓存 %d（新增 %d）' % (len(cache), added))

    con = sqlite3.connect(DB)
    changed = 0
    for table, col in TABLES:
        for rid, body in con.execute('select Id, %s from %s' % (col, table)).fetchall():
            if not body:
                continue
            out = []
            touched = False
            for line in body.split('\n'):
                s = line.strip()
                if not s:
                    out.append('')
                    continue
                marker, text = strip_marker(s)
                if text and not CJK.search(text):
                    if marker.startswith('|'):
                        cells = text.split('|')
                        new_cells = []
                        for c in cells:
                            t = c.strip()
                            new_cells.append(cache[key(t)] if t in cache or key(t) in cache else c)
                        if new_cells != cells:
                            touched = True
                        out.append(marker + '|'.join(new_cells) + ('|' if s.endswith('|') else ''))
                        continue
                    if key(text) in cache:
                        out.append(marker + cache[key(text)])
                        touched = True
                        continue
                out.append(line.rstrip())
            if touched:
                con.execute('update %s set %s = ? where Id = ?' % (table, col), ('\n'.join(out), rid))
                changed += 1
    con.commit()
    print('回写 %d 条' % changed)


def main():
    if '--export' in sys.argv:
        export()
        return 0
    if '--apply' in sys.argv:
        apply()
        return 0
    print(__doc__)
    return 0


if __name__ == '__main__':
    sys.exit(main())
