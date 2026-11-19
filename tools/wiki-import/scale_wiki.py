# -*- coding: utf-8 -*-
"""看清各类攻略页的规模：成员清单 + infobox 模板参数 + 总字数。

用来判断"能不能像任务那样结构化成表"，以及翻译量大概多少。
"""

import io
import json
import re
import sys
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0 (guide import research)'}

TARGETS = ['Combat Anomalies', 'Wormhole sites', 'Incursions sites', 'COSMOS sites',
           'DED Complexes', 'Exploration', 'Mining', 'Epic Arcs']


def get(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def members(cat, limit=500):
    out, cont = [], None
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                  'cmlimit': '500', 'cmtype': 'page', 'format': 'json'}
        if cont:
            params['cmcontinue'] = cont
        data = get(params)
        out += [m['title'] for m in data.get('query', {}).get('categorymembers', [])]
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont or len(out) >= limit:
            break
    return out[:limit]


def raw(title):
    url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
        {'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def infobox(text, names=('CMBSiteInfo', 'npcwh', 'whcobox', 'Site')):
    for name in names:
        i = text.find('{{' + name)
        if i >= 0:
            depth, j = 0, i
            while j < len(text):
                if text.startswith('{{', j):
                    depth += 1
                    j += 2
                    continue
                if text.startswith('}}', j):
                    depth -= 1
                    j += 2
                    if depth == 0:
                        return text[i:j]
                    continue
                j += 1
    return ''


def main():
    report = {}
    for cat in TARGETS:
        try:
            pages = members(cat)
        except Exception as ex:  # noqa: BLE001
            print('%-20s 取成员失败 %s' % (cat, ex))
            continue
        chars, ok, boxes = 0, 0, 0
        sample_title, sample_box = '', ''
        for t in pages:
            try:
                text = raw(t)
            except Exception:  # noqa: BLE001
                continue
            chars += len(text)
            ok += 1
            box = infobox(text)
            if box:
                boxes += 1
                if not sample_box:
                    sample_title, sample_box = t, box
        report[cat] = {'pages': len(pages), 'fetched': ok, 'chars': chars,
                       'infobox': boxes, 'avg': chars // max(1, ok),
                       'sample': pages[:8]}
        print('\n=== Category:%s ===' % cat)
        print('  页面 %d（取到 %d），总 %d 字符，平均 %d 字符/页，带 infobox %d 页'
              % (len(pages), ok, chars, chars // max(1, ok), boxes))
        print('  样例：', ' | '.join(pages[:6]))
        if sample_box:
            print('  infobox 样例（%s）：' % sample_title)
            print('   ', sample_box.replace('\n', '\n    ')[:600])

    json.dump(report, io.open('wiki_scale.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n→ wiki_scale.json')


if __name__ == '__main__':
    sys.exit(main())
