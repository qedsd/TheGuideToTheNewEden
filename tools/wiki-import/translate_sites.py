# -*- coding: utf-8 -*-
"""把 `CombatSite` 里仍是英文的正文逐行翻译掉（直接改库，不依赖重新渲染）。

之前的站点翻译走的是"重跑 build 脚本 + 译文缓存"，但站点的正文来源有三条
（wikitext / 渲染 HTML / DB 直接写入），重跑成本高且容易丢改动。
这里改成**面向数据库**：抽出英文片段 → 翻译 → 按行/按单元格替换回 WikiContent。

用法：
    python translate_sites.py --export            # 导出待译片段
    python translate_sites.py --apply             # 合并 to_translate6/zh_*.json 并回写 DB
    python translate_sites.py --export --only 9   # 只处理某个分类（可多次）
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
# --missions 时改处理 Mission 表（正文列叫 WikiContent，同样结构）
TARGETS = {
    'CombatSite': 'WikiContent',
    'Mission': 'WikiContent',
}
CACHE_FILE = os.path.join(HERE, 'translations_sites_db.json')
BATCH_DIR = os.path.join(HERE, 'to_translate6')

CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
BATCH_CHARS = 9000


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def is_fragment(text):
    s = text.strip()
    if not s or CJK.search(s):
        return False
    if s.startswith('>') or s.startswith('#') or s.startswith('**分类**'):
        return False
    return len(LATIN.findall(s)) >= 2


def collect(content):
    """从一段 Markdown 里抽英文片段：普通行整行，表格行按单元格。"""
    out = []
    for line in (content or '').split('\n'):
        s = line.strip()
        if not s:
            continue
        if s.startswith('|---'):
            continue
        if s.startswith('|'):
            # 表格单元格默认不抽（大量是 NPC 名/数字，翻了也是原样返回，白跑 agent）；
            # 只有 --cells 时才收。
            if '--cells' not in sys.argv:
                continue
            for cell in s.split('|'):
                c = cell.strip()
                if c and not CJK.search(c) and LATIN.search(c) and len(c) > 1:
                    out.append(c)
            continue
        if s.startswith('### '):
            s = s[4:].strip()
        marker = ''
        m = re.match(r'^(\*\*|\*|-)\s+', s)
        if m:
            marker = m.group(1) + ' '
            s = s[m.end():].strip()
        if is_fragment(s):
            out.append(s)
    return out


def load_cache():
    return json.load(io.open(CACHE_FILE, encoding='utf-8')) if os.path.exists(CACHE_FILE) else {}


def table_and_col():
    if '--missions' in sys.argv:
        return 'Mission', 'WikiContent'
    return 'CombatSite', 'WikiContent'


def export(only=None):
    cache = load_cache()
    con = sqlite3.connect(DB)
    table, col = table_and_col()
    if table == 'Mission':
        rows = con.execute('select Id, 0, %s from Mission' % col).fetchall()
    else:
        rows = con.execute('select Id, Category, %s from CombatSite' % col).fetchall()
    seen = {}
    for rid, cat, content in rows:
        if only and cat not in only:
            continue
        for frag in collect(content):
            if key(frag) in cache:
                continue
            seen.setdefault(frag, None)

    entries = sorted(seen)
    os.makedirs(BATCH_DIR, exist_ok=True)
    for old in os.listdir(BATCH_DIR):
        if old.startswith('batch_') or old.startswith('zh_'):
            os.remove(os.path.join(BATCH_DIR, old))

    batch, size, idx = [], 0, 0
    for text in entries:
        if batch and size + len(text) > BATCH_CHARS:
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
    print('待译 %d 条 / %d 字符 → %d 批（%s）'
          % (len(entries), sum(len(e) for e in entries), idx, BATCH_DIR))


def merge_cache():
    cache = load_cache()
    added = 0
    for zh in sorted(glob.glob(os.path.join(BATCH_DIR, 'zh_*.json'))):
        for k, v in json.load(io.open(zh, encoding='utf-8')).items():
            v = (v or '').strip()
            if v and CJK.search(v):
                if k not in cache:
                    added += 1
                cache[k] = v
    json.dump(cache, io.open(CACHE_FILE, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文缓存 %d 条（新增 %d）' % (len(cache), added))
    return cache


def apply_text(content, cache):
    """按行/单元格把英文换成中文（只替换命中缓存的片段）。"""
    out = []
    for line in (content or '').split('\n'):
        s = line.strip()
        if not s:
            out.append('')
            continue
        if s.startswith('|---'):
            out.append(s)
            continue
        if s.startswith('|'):
            cells = s.split('|')
            new = []
            for c in cells:
                t = c.strip()
                if t and not CJK.search(t) and key(t) in cache:
                    new.append(' ' + cache[key(t)] + ' ')
                else:
                    new.append(c)
            out.append('|'.join(new))
            continue
        prefix = ''
        if s.startswith('### '):
            prefix, probe = '### ', s[4:].strip()
        else:
            m = re.match(r'^(\*\*|\*|-)\s+', s)
            prefix, probe = (m.group(0), s[m.end():].strip()) if m else ('', s)
        if probe and not CJK.search(probe) and key(probe) in cache:
            out.append(prefix + cache[key(probe)])
            continue
        out.append(line.rstrip())
    return '\n'.join(out)


def apply_changes():
    cache = merge_cache()
    con = sqlite3.connect(DB)
    table, col = table_and_col()
    rows = con.execute('select Id, %s from %s' % (col, table)).fetchall()
    changed = 0
    for rid, content in rows:
        if not content:
            continue
        new = apply_text(content, cache)
        if new != content:
            con.execute('update %s set %s = ? where Id = ?' % (table, col), (new, rid))
            changed += 1
    con.commit()
    print('回写 %d 条' % changed)


def main():
    only = None
    if '--only' in sys.argv:
        i = sys.argv.index('--only')
        only = {int(x) for x in sys.argv[i + 1].split(',')}
    if '--export' in sys.argv:
        export(only)
        return 0
    if '--apply' in sys.argv:
        apply_changes()
        return 0
    print(__doc__)
    return 0


if __name__ == '__main__':
    sys.exit(main())
