# -*- coding: utf-8 -*-
"""抓 COSMOS 相关 wiki 页面，弄清它到底是什么类型的内容。"""

import io
import json
import re
import sys
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0 (guide research)'}


def get(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def raw(title):
    url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
        {'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def plain(text, limit=3000):
    """粗清 wiki 标记，便于阅读。"""
    text = re.sub(r'\{\{[^{}]*\}\}', '', text)
    text = re.sub(r'\[\[(?:[^\]|]*\|)?([^\]]+)\]\]', r'\1', text)
    text = re.sub(r'<[^>]+>', '', text)
    text = re.sub(r"'{2,}", '', text)
    text = re.sub(r'(?m)^[=]{2,}\s*(.+?)\s*[=]{2,}$', r'\n## \1', text)
    text = re.sub(r'\n{3,}', '\n\n', text)
    return text.strip()[:limit]


def main():
    pages = ['COSMOS', 'COSMOS missions', 'COSMOS agents', 'Amarr COSMOS', 'Caldari COSMOS',
             'Gallente COSMOS', 'Minmatar COSMOS']
    for title in pages:
        try:
            text = raw(title)
        except Exception as ex:  # noqa: BLE001
            print('\n===== %s =====\n  取不到: %s' % (title, ex))
            continue
        print('\n===== %s（%d 字符）=====' % (title, len(text)))
        print(plain(text, 2600))


if __name__ == '__main__':
    sys.exit(main())
