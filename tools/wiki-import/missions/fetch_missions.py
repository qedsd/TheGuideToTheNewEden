# -*- coding: utf-8 -*-
"""抓 EVE University Wiki 的任务页（原始 wikitext），存到本目录下。

为什么用 action=raw 而不是渲染后的 HTML：任务页的数据几乎全在 `{{Missiondetails|…}}` 里，
wikitext 直接就是一串 k=v，比从 HTML 反推表格可靠得多（战斗站点那套是反过来的：那边数据在
渲染后的表格里）。

产物：
  index.json   —— 每页的 title / category / pageid / 抓取时间
  raw/<safe>.txt —— 原始 wikitext（页面标题清洗后的文件名）

用法：
  python fetch_missions.py            # 增量（已存在且能对上 pageid 的跳过）
  python fetch_missions.py --force    # 全量重抓
"""

import io
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
RAW_DIR = os.path.join(HERE, 'raw')
INDEX = os.path.join(HERE, 'index.json')

API = 'https://wiki.eveuniversity.org/api.php'
UA = 'TheGuideToTheNewEden/1.0 (offline guide importer; contact: local)'
DELAY = 0.15

# (wiki 分类, 本项目分类 id, 中文名)
CATEGORIES = [
    ('Security missions', 0, '安全任务'),
    ('Distribution Missions', 1, '运输任务'),
    ('Mining Missions', 2, '采矿任务'),
    ('Storyline missions', 3, '故事线任务'),
    ('Epic Arcs', 4, '史诗任务线'),
    ('Anomic missions', 5, '异常/燃烧者任务'),
    ('COSMOS', 6, 'COSMOS 任务'),
    ('R&D Missions', 7, '研发任务'),
    ('Career Agents', 8, '职业代理人任务'),
    ('Circle missions', 9, 'Circle 任务'),
]

# 分类里混着的"目录页/总览页"，不是具体任务
SKIP_TITLES = {
    'Anomic missions', 'COSMOS', 'Career Agents', 'Circle Missions', 'Epic arcs',
    'Epic Arcs', 'Security missions', 'Distribution missions', 'Mining missions',
    'Storyline missions', 'Research missions', 'Data center', 'Distribution mission reports',
}


def safe_name(title):
    """页面标题 → 文件名（Windows 不允许的字符换成全角，并在 index 里保留原名）。"""
    table = {
        '/': '／', '\\': '＼', ':': '：', '*': '＊', '?': '？', '"': '＂',
        '<': '＜', '>': '＞', '|': '｜',
    }
    out = ''.join(table.get(ch, ch) for ch in title)
    return out.strip()[:150]


def get(url, tries=3):
    for i in range(tries):
        try:
            req = urllib.request.Request(url, headers={'User-Agent': UA})
            with urllib.request.urlopen(req, timeout=40) as r:
                return r.read().decode('utf-8', 'replace')
        except Exception as ex:  # noqa: BLE001
            if i == tries - 1:
                raise
            print('   重试 %d/%d: %s' % (i + 1, tries, ex))
            time.sleep(2 + i * 2)
    return ''


def category_pages(cat):
    """翻页取完一个分类下的全部条目（cmcontinue）。"""
    pages = []
    cont = None
    while True:
        params = {
            'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
            'cmlimit': '500', 'cmtype': 'page', 'format': 'json',
        }
        if cont:
            params['cmcontinue'] = cont
        url = API + '?' + urllib.parse.urlencode(params)
        data = json.loads(get(url))
        for m in data.get('query', {}).get('categorymembers', []):
            pages.append(m['title'])
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont:
            break
        time.sleep(DELAY)
    return pages


def raw_wikitext(title):
    url = ('https://wiki.eveuniversity.org/index.php?title='
           + urllib.parse.quote(title.replace(' ', '_')) + '&action=raw')
    return get(url)


def main():
    force = '--force' in sys.argv
    os.makedirs(RAW_DIR, exist_ok=True)

    old = {}
    if os.path.exists(INDEX):
        old = {e['title']: e for e in json.load(io.open(INDEX, encoding='utf-8'))}

    entries = []
    seen = set()
    fetched = skipped = failed = 0

    for cat, cat_id, cat_cn in CATEGORIES:
        titles = [t for t in category_pages(cat) if t not in SKIP_TITLES]
        print('%-24s %3d 页' % (cat, len(titles)))
        for title in titles:
            if title in seen:
                continue
            seen.add(title)

            path = os.path.join(RAW_DIR, safe_name(title) + '.txt')
            prev = old.get(title)
            if (not force and prev and prev.get('cat') == cat_id
                    and os.path.exists(path) and os.path.getsize(path) > 0):
                entries.append(prev)
                skipped += 1
                continue

            try:
                text = raw_wikitext(title)
            except Exception as ex:  # noqa: BLE001
                print('   FAIL %s: %s' % (title, ex))
                failed += 1
                continue

            if not text.strip():
                print('   空页，跳过：%s' % title)
                failed += 1
                continue

            io.open(path, 'w', encoding='utf-8', newline='').write(text)
            entries.append({
                'title': title,
                'cat': cat_id,
                'catCn': cat_cn,
                'file': os.path.basename(path),
                'chars': len(text),
            })
            fetched += 1
            time.sleep(DELAY)

    entries.sort(key=lambda e: (e['cat'], e['title']))
    json.dump(entries, io.open(INDEX, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    by_cat = {}
    for e in entries:
        by_cat[e['catCn']] = by_cat.get(e['catCn'], 0) + 1
    print('\n共 %d 页（新抓 %d，跳过 %d，失败 %d）' % (len(entries), fetched, skipped, failed))
    for k, v in by_cat.items():
        print('  %-16s %3d' % (k, v))
    total = sum(e['chars'] for e in entries)
    print('  原始文本合计 %.1f 万字符' % (total / 10000.0))
    return 0


if __name__ == '__main__':
    sys.exit(main())
