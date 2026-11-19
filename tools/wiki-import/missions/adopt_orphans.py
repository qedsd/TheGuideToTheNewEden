# -*- coding: utf-8 -*-
"""把"抓下来了但不在 index 里"的页面补进 index，让主解析器（能读 infobox 字段）处理它们。

这批页面（约 116 个）是 `fetch_cosmos_missions.py` 直接抓的，文件名是下划线形式，
而 index 用的是真实标题（带空格和括号）。这里按文件名反推标题，用 API 校验存在性后：
  ① 把 raw 文件改名成 index 的命名
  ② 往 index.json 追加条目（分类固定为 COSMOS=6）
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
RAW = os.path.join(HERE, 'raw')
INDEX = os.path.join(HERE, 'index.json')
API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}


def get_json(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def candidates(stem):
    """`Activist_Fuel_1_of_4` → ['Activist Fuel (1 of 4)', 'Activist Fuel 1 of 4', …]"""
    s = stem.replace('_', ' ').strip()
    out = []
    m = re.search(r'^(.*?)\s*(\d+)\s+of\s+(\d+)$', s)
    if m:
        base, a, b = m.group(1).strip(), m.group(2), m.group(3)
        out += ['%s (%s of %s)' % (base, a, b), '%s %s of %s' % (base, a, b)]
    out.append(s)
    return list(dict.fromkeys(out))


def main():
    entries = json.load(io.open(INDEX, encoding='utf-8'))
    known_files = {e['file'] for e in entries}
    known_titles = {e['title'].lower() for e in entries}

    orphans = [f for f in os.listdir(RAW)
               if f.endswith('.txt') and f not in known_files]
    print('待处理文件 %d 个' % len(orphans))

    added = 0
    for fn in orphans:
        stem = os.path.splitext(fn)[0]
        titles = candidates(stem)
        found = None
        data = get_json({'action': 'query', 'titles': '|'.join(titles), 'format': 'json',
                         'redirects': '1'})
        pages = data.get('query', {}).get('pages', {})
        for pid, page in pages.items():
            if int(pid) > 0 and page.get('title'):
                found = page['title']
                break
        if not found or found.lower() in known_titles:
            if found:
                os.remove(os.path.join(RAW, fn))     # 已有同名页面，删掉副本
            continue
        new_name = found + '.txt'
        os.replace(os.path.join(RAW, fn), os.path.join(RAW, new_name))
        entries.append({'title': found, 'cat': 6, 'catCn': '空中代理人',
                        'file': new_name, 'chars': os.path.getsize(os.path.join(RAW, new_name)),
                        'wikiCategory': ''})
        known_files.add(new_name)
        known_titles.add(found.lower())
        added += 1
        print('  + %-52s ← %s' % (found[:52], fn))
        time.sleep(0.2)

    json.dump(entries, io.open(INDEX, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n新增 %d 条，index 共 %d 页' % (added, len(entries)))


if __name__ == '__main__':
    sys.exit(main())
