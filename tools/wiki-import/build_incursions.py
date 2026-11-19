# -*- coding: utf-8 -*-
"""把入侵站点写成数据库表 `IncursionSite`。

数据来源：`incursions_parsed.json`（渲染 HTML 解析出来的小节 + 表格）。
译文：`translations_incursions.json`（按行存，key = sha1(英文行)[:16]），缺译回退英文。

用法：
    python build_incursions.py              # 只生成 incursions_sites.json
    python build_incursions.py --write-db   # 写库（DROP + CREATE IncursionSite）
    python build_incursions.py --export     # 导出待翻译片段到 to_translate3/
    python build_incursions.py --merge      # 合并 to_translate3/zh_*.json
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
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
STATIC_DB = os.path.join(ROOT, 'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF',
                         'Resources', 'Database', 'static.db')
PARSED = os.path.join(HERE, 'incursions_parsed.json')
TRANS = os.path.join(HERE, 'translations_incursions.json')
BATCH_DIR = os.path.join(HERE, 'to_translate3')
OUT_JSON = os.path.join(HERE, 'incursions_sites.json')
SYNC_DATE = '2026-11-18'

# 站点类型：0 集结点 1 先锋 2 突击 3 总部 4 母舰
TYPE_CN = {0: '集结点', 1: '先锋站点', 2: '突击站点', 3: '总部站点', 4: '母舰站点'}
TYPE_BY_KEY = {'staging sites': 0, 'staging': 0, 'vanguards': 1, 'vanguard': 1,
               'assault': 2, 'assaults': 2, 'headquarters': 3, 'headquarter sites': 3,
               'mothership site': 4, 'mothership': 4}
STAGING_TITLES = {'distress beacon', 'forward reconnaissance outpost',
                  'nation industrial proxy', 'propaganda cluster'}
CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
TABLE_LINE = re.compile(r'^\|')
TRANS_CACHE = {}
_is = None
IT = os.path.join(HERE, 'translations_incursions.json')


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def tr(text):
    if not text:
        return ''
    t = text.strip()
    hit = TRANS_CACHE.get(key(t))
    return hit if hit else t


def tr_block(text):
    out = []
    for line in (text or '').split('\n'):
        s = line.strip()
        if not s:
            out.append('')
            continue
        if s.startswith('|---'):
            out.append(s)
            continue
        if s.startswith('|'):
            # 表格行逐格翻译（表头 Sig/Speed/Orbit 这些也要中文化）
            cells = s.split('|')
            out.append('|'.join(tr(c.strip()) if c.strip() and not CJK.search(c) else c
                                for c in cells))
            continue
        if CJK.search(s):
            out.append(line.rstrip())
            continue
        out.append(tr(s))
    return '\n'.join(out).strip()


def type_of(page):
    t = (page.get('type') or '').strip().lower()
    if t in TYPE_BY_KEY:
        return TYPE_BY_KEY[t]
    if page['title'].lower() in STAGING_TITLES:
        return 0
    return -1


def render(page, code):
    out = []
    meta = ['**类型** %s' % TYPE_CN.get(code, '入侵站点'), '**势力** 萨沙国度']
    out.append(' ｜ '.join(meta))
    out.append('')
    for sec in page['sections']:
        title = (sec.get('title') or '').strip()
        text = (sec.get('text') or '').strip()
        if not title and not text:
            continue
        # 只有标题没有正文的小节不渲染（页面上会变成孤立标题）
        if title and not text:
            continue
        # 站点导航表（每页开头都有一份同样的清单）跳过
        if title.lower() == 'incursion sites':
            continue
        if title:
            out.append('### ' + tr(title))
            out.append('')
        if text:
            # 每页开头那张"全部站点导航表"没有信息量，丢掉
            if 'Click the title for a summary' in text:
                continue
            out.append(tr_block(text))
            out.append('')
    body = '\n'.join(out).strip()
    return body + '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC_DATE


def build():
    pages = json.load(io.open(PARSED, encoding='utf-8'))
    records = []
    rid = 0
    for page in pages:
        if page['title'].lower() == 'incursion sites':
            continue           # 枢纽页，不是站点
        code = type_of(page)
        rid += 1
        records.append({
            'Id': rid,
            'Type': code,
            'TypeCN': TYPE_CN.get(code, ''),
            'TitleEN': page['title'],
            'TitleCN': tr(page['title']),
            'Content': render(page, code),
            'IsStub': len(page.get('sections') or []) == 0,
            'WikiUpdatedAt': SYNC_DATE,
        })
    return records


def export():
    records = build()
    seen = {}
    for r in records:
        # 站点名也要译
        if r['TitleEN'] and not CJK.search(r['TitleEN']):
            seen[r['TitleEN']] = True
        for line in r['Content'].split('\n'):
            s = line.strip()
            if s.startswith('|---'):
                continue
            if s.startswith('|'):
                # 表格：逐格收集（表头、说明文字都要译）
                for cell in s.split('|'):
                    c = cell.strip()
                    if c and not CJK.search(c) and len(LATIN.findall(c)) >= 1 and key(c) not in TRANS_CACHE:
                        seen[c] = True
                continue
            if s.startswith('### '):
                s = s[4:].strip()      # 小节标题也要译（之前漏了，页面上还是英文标题）
            if not s or s.startswith('>') or s.startswith('#'):
                continue
            if CJK.search(s):
                continue
            # 小节标题常常只有一个词（Objectives / Enemies），不能按"≥2 个英文词"过滤
            if len(LATIN.findall(s)) < 2 and len(s.split()) > 2:
                continue
            if key(s) not in TRANS_CACHE:
                seen[s] = True
    items = sorted(seen)
    os.makedirs(BATCH_DIR, exist_ok=True)
    for old in os.listdir(BATCH_DIR):
        os.remove(os.path.join(BATCH_DIR, old))
    batch, size, idx = [], 0, 0
    for text in items:
        if batch and size + len(text) > 9000:
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
    print('待翻译 %d 行 / %d 字符 → %d 批（%s）'
          % (len(items), sum(len(i) for i in items), idx, BATCH_DIR))


def merge():
    added = 0
    for zh_path in sorted(glob.glob(os.path.join(BATCH_DIR, 'zh_*.json'))):
        data = json.load(io.open(zh_path, encoding='utf-8'))
        for k, v in data.items():
            v = (v or '').strip()
            if v and CJK.search(v):
                if k not in TRANS_CACHE:
                    added += 1
                TRANS_CACHE[k] = v
    json.dump(TRANS_CACHE, io.open(IT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文缓存 %d 条（新增 %d）→ %s' % (len(TRANS_CACHE), added, IT))


def write_db(records):
    con = sqlite3.connect(STATIC_DB)
    cur = con.cursor()
    cur.execute('DROP TABLE IF EXISTS IncursionSite')
    cur.execute('''CREATE TABLE IncursionSite (
        Id INTEGER PRIMARY KEY,
        Type INTEGER,
        TypeCN TEXT,
        TitleEN TEXT,
        TitleCN TEXT,
        Content TEXT,
        IsStub INTEGER,
        WikiUpdatedAt TEXT)''')
    cur.executemany(
        'INSERT INTO IncursionSite (Id,Type,TypeCN,TitleEN,TitleCN,Content,IsStub,WikiUpdatedAt) '
        'VALUES (?,?,?,?,?,?,?,?)',
        [(r['Id'], r['Type'], r['TypeCN'], r['TitleEN'], r['TitleCN'], r['Content'],
          r['IsStub'], r['WikiUpdatedAt']) for r in records])
    con.commit()
    print('写入 IncursionSite %d 条' % len(records))


def main():
    global TRANS_CACHE
    TRANS_CACHE = json.load(io.open(IT, encoding='utf-8')) if os.path.exists(IT) else {}

    if '--export' in sys.argv:
        export()
        return 0
    if '--merge' in sys.argv:
        merge()
        return 0

    records = build()
    json.dump(records, io.open(OUT_JSON, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    by_type = {}
    for r in records:
        by_type[r['TypeCN']] = by_type.get(r['TypeCN'], 0) + 1
    print('生成 %d 个入侵站点：' % len(records))
    for k, v in sorted(by_type.items(), key=lambda kv: -kv[1]):
        print('  %-10s %d' % (k, v))
    if '--write-db' in sys.argv:
        write_db(records)
    return 0


if __name__ == '__main__':
    sys.exit(main())
