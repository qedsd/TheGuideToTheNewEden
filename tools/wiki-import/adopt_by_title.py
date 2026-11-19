# -*- coding: utf-8 -*-
"""按**标题**直接补抓几页——用于那些不在 wiki 分类清单里的漏页。

用法：python adopt_by_title.py
（脚本里的 TARGETS 就是待补清单，补完跑对应 build 脚本即可）
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
API = 'https://wiki.eveuniversity.org/index.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}

# (标题, 目标目录, index 文件名, 是否 HTML)
TARGETS = [
    ("Gurista Productions Shipment", 'extra_sites', 'index_expedition.json', False),
    ("Serpentis Logistical Outpost Part 2", 'extra_sites', 'index_expedition.json', False),
    ("Shadow Serpentis Fleet Staging Point 2 and part 3", 'extra_sites', 'index_expedition.json', False),
    ("Caldari Agent's Hideout", 'cosmos_sites', 'index.json', False),
    ("Roaving Brigands", 'cosmos_sites', 'index.json', False),
    ("Tsuna's Private Compounds", 'cosmos_sites', 'index.json', False),
]


def slug(title):
    return re.sub(r'[^\w\u4e00-\u9fff]+', '_', title)


def fetch(title):
    url = API + '?' + urllib.parse.urlencode({'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.read().decode('utf-8', 'replace')


def main():
    ok = 0
    for title, folder, index_name, _html in TARGETS:
        raw_dir = os.path.join(HERE, 'raw', folder)
        os.makedirs(raw_dir, exist_ok=True)
        index_path = os.path.join(raw_dir, index_name)
        index = json.load(io.open(index_path, encoding='utf-8')) if os.path.exists(index_path) else {}

        prefix = 'expedition__' if folder == 'extra_sites' else ''
        fn = prefix + slug(title) + '.wiki'
        path = os.path.join(raw_dir, fn)
        if not os.path.exists(path):
            try:
                text = fetch(title)
            except Exception as ex:  # noqa: BLE001
                print('  FAIL %-52s %s' % (title, ex))
                continue
            io.open(path, 'w', encoding='utf-8').write(text)
            time.sleep(0.3)
        size = os.path.getsize(path)
        index[fn] = title
        json.dump(index, io.open(index_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        ok += 1
        print('  OK   %-52s %6d 字节' % (title, size))
    print('\n补抓 %d 页' % ok)


if __name__ == '__main__':
    sys.exit(main())
