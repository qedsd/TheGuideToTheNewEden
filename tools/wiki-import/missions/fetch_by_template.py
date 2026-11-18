# -*- coding: utf-8 -*-
"""按**模板转写**枚举全部任务页并补抓。

为什么不再以"分类"为准：`Category:Security missions` 用 `cmlimit=500` 翻页时漏了一大截
（实抓 717 页，实际更多），而 `Category:Epic arc missions`（真正的史诗任务）根本不在
`Category:Epic Arcs`（那个只是章节枢纽）里。结果就是"腹背受敌 / Enemies Abound"这种
四帝国势力的任务整批缺失。

权威口径是 MediaWiki 的 `list=embeddedin`：谁转写了 `Template:Missiondetails` 谁就是任务页，
一共 1741 页。分类只用来**归类**，不再用来枚举。

用法：
  python fetch_by_template.py            # 增量补抓
  python fetch_by_template.py --force    # 全部重抓
"""

import io
import json
import os
import sys
import time
import urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from fetch_missions import RAW_DIR, INDEX, get, raw_wikitext, safe_name, API, DELAY  # noqa: E402

# wiki 分类名 → 本项目分类 id（比 fetch_missions.py 的 CATEGORIES 更全）
CATEGORY_MAP = {
    'security missions': 0,
    'distribution missions': 1,
    'mining missions': 2,
    'storyline missions': 3,
    'epic arcs': 4,
    'epic arc missions': 4,
    'anomic missions': 5,
    'cosmos': 6,
    'r&d missions': 7,
    'career agents': 8,
    'circle missions': 9,
    'circle missions': 9,
}

CATEGORY_CN = {0: '安全任务', 1: '运输任务', 2: '采矿任务', 3: '故事线任务', 4: '史诗任务',
               5: '异常任务', 6: 'COSMOS 任务', 7: '研发任务', 8: '职业代理人任务', 9: 'Circle 任务'}


def template_pages(template='Template:Missiondetails'):
    """转写了某个模板的全部页面（含翻页）。"""
    pages = []
    cont = None
    while True:
        params = {
            'action': 'query', 'list': 'embeddedin', 'eititle': template,
            'eilimit': '500', 'einamespace': '0', 'format': 'json',
        }
        if cont:
            params['eicontinue'] = cont
        data = json.loads(get(API + '?' + urllib.parse.urlencode(params)))
        for m in data.get('query', {}).get('embeddedin', []):
            pages.append(m['title'])
        cont = data.get('continue', {}).get('eicontinue')
        if not cont:
            break
        time.sleep(DELAY)
    return pages


def categories_of(titles):
    """批量取页面分类（每批 50 个标题）。返回 {title: [category, …]}。"""
    out = {}
    for i in range(0, len(titles), 50):
        batch = titles[i:i + 50]
        params = {
            'action': 'query', 'prop': 'categories', 'cllimit': 'max', 'format': 'json',
            'titles': '|'.join(batch),
        }
        data = json.loads(get(API + '?' + urllib.parse.urlencode(params)))
        for _pid, page in (data.get('query', {}).get('pages') or {}).items():
            title = page.get('title')
            if not title:
                continue
            out[title] = [c['title'] for c in page.get('categories', [])]
        time.sleep(DELAY)
    return out


def pick_category(cats):
    """分类列表 → 本项目分类 id；命中多个时按"越特殊越优先"取。"""
    lows = [c.replace('Category:', '').strip().lower() for c in cats]
    for key in ('anomic missions', 'epic arc missions', 'epic arcs', 'cosmos',
                'r&d missions', 'career agents', 'circle missions',
                'storyline missions', 'mining missions', 'distribution missions',
                'security missions'):
        if key in lows:
            return CATEGORY_MAP[key], key
    return None, ''


def main():
    force = '--force' in sys.argv
    os.makedirs(RAW_DIR, exist_ok=True)

    entries = json.load(io.open(INDEX, encoding='utf-8')) if os.path.exists(INDEX) else []
    known = {e['title']: e for e in entries}

    titles = template_pages()
    print('模板转写命中 %d 页，本地已有 %d 页，待补 %d 页'
          % (len(titles), len(known), len([t for t in titles if t not in known])))

    todo = [t for t in titles if force or t not in known]
    if not todo:
        print('没有需要补抓的页面。')
        return 0

    # 先批量拿分类，避免每页一次请求
    cats = categories_of(todo)
    print('已取到 %d 页的分类信息' % len(cats))

    added = failed = uncategorized = 0
    for title in todo:
        try:
            text = raw_wikitext(title)
        except Exception as ex:  # noqa: BLE001
            print('   FAIL %s: %s' % (title, ex))
            failed += 1
            continue
        if not text.strip():
            failed += 1
            continue

        path = os.path.join(RAW_DIR, safe_name(title) + '.txt')
        io.open(path, 'w', encoding='utf-8', newline='').write(text)

        cat_id, raw_cat = pick_category(cats.get(title, []))
        if cat_id is None:
            cat_id = 0          # 暂时归到安全任务，build 阶段会按 Type 再纠正
            uncategorized += 1

        entry = {
            'title': title,
            'cat': cat_id,
            'catCn': CATEGORY_CN[cat_id],
            'file': os.path.basename(path),
            'chars': len(text),
            'wikiCategory': raw_cat,
        }
        known[title] = entry
        added += 1
        time.sleep(DELAY)

    entries = sorted(known.values(), key=lambda e: (e['cat'], e['title']))
    json.dump(entries, io.open(INDEX, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    by_cat = {}
    for e in entries:
        by_cat[e['catCn']] = by_cat.get(e['catCn'], 0) + 1
    print('\n新增 %d 页（失败 %d，无已知分类 %d），index 共 %d 页'
          % (added, failed, uncategorized, len(entries)))
    for k, v in sorted(by_cat.items()):
        print('  %-16s %4d' % (k, v))
    return 0


if __name__ == '__main__':
    sys.exit(main())
