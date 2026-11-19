# -*- coding: utf-8 -*-
"""把 wiki 分类清单和本地库里已有的数据对一遍，算清楚"哪些还没收、要多少翻译量"。"""

import io
import json
import os
import sqlite3
import sys
import urllib.parse
import urllib.request

API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0 (guide import research)'}
DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
      r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")


def get(params):
    url = API + '?' + urllib.parse.urlencode(params)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode('utf-8'))


def members(cat, limit=1000):
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


def sizes(titles):
    """批量取页面字节数（prop=info 里没有 size，用 list=allpages? 简化：取 revisions size）。"""
    total = 0
    for i in range(0, len(titles), 50):
        batch = titles[i:i + 50]
        data = get({'action': 'query', 'prop': 'revisions', 'rvprop': 'size',
                    'titles': '|'.join(batch), 'format': 'json'})
        for _pid, page in data.get('query', {}).get('pages', {}).items():
            for rev in page.get('revisions') or []:
                total += rev.get('size') or 0
                break
    return total


def main():
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute("select lower(TitleEN) from CombatSite")
    have_sites = {r[0] for r in cur.fetchall()}
    cur.execute("select lower(TitleEN) from Mission")
    have_missions = {r[0] for r in cur.fetchall()}

    plans = {
        'Combat Anomalies': ('combat_site', '异常点'),
        'DED Complexes': ('combat_site', 'DED 复合体'),
        'COSMOS sites': ('combat_site', 'COSMOS 站点'),
        'COSMOS missions': ('mission', 'COSMOS 任务'),
        'COSMOS agents': ('none', 'COSMOS 代理人'),
        'Wormhole sites': ('none', '虫洞站点'),
        'Incursions sites': ('none', '入侵站点'),
        'Exploration': ('none', '探索指南'),
        'Mining': ('none', '采矿指南'),
        'Faction Warfare': ('none', '势力战争'),
        'Fitting': ('none', '推荐配置'),
    }

    rows = []
    for cat, (kind, label) in plans.items():
        try:
            pages = members(cat)
        except Exception as ex:  # noqa: BLE001
            rows.append((label, cat, -1, -1, '取成员失败: %s' % ex))
            continue
        if kind == 'combat_site':
            have = sum(1 for p in pages if p.lower() in have_sites)
        elif kind == 'mission':
            have = sum(1 for p in pages if p.lower() in have_missions)
        else:
            have = 0
        total = sizes(pages)
        rows.append((label, cat, len(pages), total, '%d 页已有数据（%.0f%%），待补 %d 页'
                     % (have, 100.0 * have / max(1, len(pages)), len(pages) - have)))

    print('%-14s %-22s %5s %9s  %s' % ('板块', '分类', '页数', '字节', '本地覆盖'))
    for label, cat, n, total, note in rows:
        print('%-14s %-22s %5d %9d  %s' % (label, cat[:22], n, total, note))

    json.dump([{'label': l, 'cat': c, 'pages': n, 'bytes': b, 'note': note}
               for l, c, n, b, note in rows],
              io.open('wiki_gap.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n→ wiki_gap.json')


if __name__ == '__main__':
    sys.exit(main())
