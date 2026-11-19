# -*- coding: utf-8 -*-
"""抓入侵站点（Incursions sites）页面。

入侵站点的关键信息（舰队需求、敌人构成）是**模板生成**的，抓 wikitext 只能看到
`{{incursion sites|assaults|ncn}}` 这样的调用，所以这里抓**渲染后的 HTML**，
再把正文和表格转成 Markdown。

产物：
  raw/incursions/<Title>.html   渲染后 HTML
  raw/incursions/index.json     站点清单
"""

import io
import json
import os
import sys
import time
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
BASE = 'https://wiki.eveuniversity.org'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0 (guide import)'}
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'raw', 'incursions')
DELAY = 0.4


def get_json(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def members(cat):
    out, cont = [], None
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                  'cmlimit': '500', 'cmtype': 'page', 'format': 'json'}
        if cont:
            params['cmcontinue'] = cont
        data = get_json(params)
        out += [m['title'] for m in data.get('query', {}).get('categorymembers', [])]
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont:
            break
    return out


def rendered_html(title):
    """整页渲染后的 HTML（模板已展开）。"""
    url = BASE + '/index.php?' + urllib.parse.urlencode({'title': title, 'action': 'render'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def main():
    os.makedirs(OUT, exist_ok=True)
    titles = [t for t in members('Incursions sites') if not t.startswith('User:')]
    print('入侵站点 %d 个（已排除用户沙盒）' % len(titles))
    index = []
    for title in titles:
        path = os.path.join(OUT, title.replace('/', '_').replace(' ', '_') + '.html')
        if os.path.exists(path) and '--force' not in sys.argv:
            index.append({'title': title, 'file': os.path.basename(path)})
            print('  跳过（已存在）', title)
            continue
        try:
            html = rendered_html(title)
        except Exception as ex:  # noqa: BLE001
            print('  FAIL %s: %s' % (title, ex))
            continue
        io.open(path, 'w', encoding='utf-8').write(html)
        index.append({'title': title, 'file': os.path.basename(path), 'chars': len(html)})
        print('  %-46s %6d 字符' % (title[:46], len(html)))
        time.sleep(DELAY)
    json.dump(index, io.open(os.path.join(OUT, 'index.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('\n→ %s/index.json' % OUT)


if __name__ == '__main__':
    sys.exit(main())
