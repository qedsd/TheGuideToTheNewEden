# -*- coding: utf-8 -*-
"""盘点 EVE University Wiki 有哪些成体系、可结构化导入的攻略板块。

输出：每个分类的页面数、子分类、样例子页面（用来判断"是结构化数据还是散文"）。
"""

import io
import json
import sys
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0 (guide import research)'}


def get(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def all_categories(prefixes, min_pages=5):
    found = {}
    for prefix in prefixes:
        data = get({'action': 'query', 'list': 'allcategories', 'acprefix': prefix,
                    'aclimit': '500', 'acprop': 'size', 'format': 'json'})
        for c in data.get('query', {}).get('allcategories', []):
            name = c.get('*') or c.get('category') or ''
            pages = c.get('pages') or 0
            if name and pages >= min_pages:
                found[name] = {'pages': pages, 'subcats': c.get('subcats') or 0}
    return found


def members(cat, limit=8):
    data = get({'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                'cmlimit': str(limit), 'cmtype': 'page', 'format': 'json'})
    return [m['title'] for m in data.get('query', {}).get('categorymembers', [])]


def main():
    prefixes = ['Combat', 'Dead', 'Worm', 'Sleeper', 'Triglavian', 'Abyssal', 'Incursion',
                'Anomal', 'Escalation', 'Ghost', 'DED', 'Mission', 'Exploration', 'Industry',
                'Mining', 'Trade', 'Haul', 'Standing', 'Ship', 'Fitting', 'Scan', 'Archaeolog',
                'Hacking', 'Epic', 'COSMOS', 'Agent', 'Faction', 'Ice', 'Moon', 'Planetary',
                'Research', 'Manufactur', 'Blueprint', 'Salvag', 'Loot', 'Ratting', 'PvE']
    cats = all_categories(prefixes)
    rows = sorted(cats.items(), key=lambda kv: -kv[1]['pages'])

    print('%-42s %6s %5s' % ('分类', '页面', '子类'))
    for name, info in rows:
        print('%-42s %6d %5d' % (name[:42], info['pages'], info['subcats']))

    out = {}
    for name, info in rows:
        try:
            out[name] = dict(info, sample=members(name))
        except Exception as ex:  # noqa: BLE001
            out[name] = dict(info, sample=[], error=str(ex))
    json.dump(out, io.open('wiki_catalog.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n完整结果 → wiki_catalog.json (%d 个分类)' % len(out))


if __name__ == '__main__':
    sys.exit(main())
