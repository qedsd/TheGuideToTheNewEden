# -*- coding: utf-8 -*-
"""统计"wiki 上还有哪些攻略类型没进库"。

对每个候选分类：拉成员页 → 取字节数 → 和库里已有的标题（Mission / CombatSite /
IncursionSite）比对 → 输出「页数 / 总KB / 已覆盖 / 缺口页 / 缺口KB」，按缺口大小排序。
"""

import io
import json
import os
import sqlite3
import sys
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
DB = os.path.abspath(os.path.join(HERE, '..', '..',
                                  'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF',
                                  'Resources', 'Database', 'static.db'))
API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}

# 候选分类：名称 → 说明（也是最终报告里的用途）
CATEGORIES = [
    ('Combat anomalies', '战斗异常'),
    ('DED complexes', 'DED 评级复合体'),
    ('Unrated complexes', '无评级复合体'),
    ('Expeditions', '远征'),
    ('Chemical labs', '化学实验室'),
    ('Relic sites', '战斗遗迹'),
    ('COSMOS sites', '空中代理人站点'),
    ('COSMOS missions', '空中代理人任务'),
    ('Incursions sites', '入侵站点'),
    ('Wormhole sites', '虫洞站点'),
    ('Wormhole systems', '虫洞星系'),
    ('Exploration', '探索（含数据/遗迹）'),
    ('Mining', '采矿'),
    ('Ice belts', '冰带'),
    ('Factional Warfare', '势力战争'),
    ('Faction warfare complexes', '势力战争复合体'),
    ('Fittings', '装配方案'),
    ('Abyssal Deadspace', '深渊死亡空间'),
    ('Triglavian sites', '三神裔站点'),
    ('Pochven', '波赫文'),
    ('Epic arcs', '史诗任务线'),
    ('Storyline missions', '故事线任务'),
    ('Anomic missions', '异常任务（Burner）'),
    ('Sisters of EVE', '姐妹会'),
    ('Sleeper sites', '冬眠者站点'),
    ('Ore sites', '矿石站点'),
    ('Gas sites', '气云站点'),
    ('Data sites', '数据站点'),
    ('Relic sites (exploration)', '遗迹站点（探索）'),
    ('LP Store', '忠诚点商店'),
    ('Skills', '技能'),
    ('Implants', '植入体'),
    ('Modules', '装备'),
    ('Ships', '舰船'),
    ('Drones', '无人机'),
    ('Structures', '建筑'),
    ('Asteroid belts', '小行星带'),
]


def get_json(params):
    url = API + '?' + urllib.parse.urlencode(params)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read().decode('utf-8'))


def members(cat, limit=3000):
    out, cont = [], None
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                  'cmlimit': '500', 'cmtype': 'page', 'format': 'json'}
        if cont:
            params['cmcontinue'] = cont
        try:
            data = get_json(params)
        except Exception:  # noqa: BLE001
            return out
        out += [m['title'] for m in data.get('query', {}).get('categorymembers', [])]
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont or len(out) >= limit:
            break
    return out[:limit]


def sizes(titles):
    """批量取页面字节数。"""
    out = {}
    for i in range(0, len(titles), 40):
        chunk = titles[i:i + 40]
        data = get_json({'action': 'query', 'titles': '|'.join(chunk), 'prop': 'info',
                         'format': 'json', 'redirects': '1'})
        for pid, page in data.get('query', {}).get('pages', {}).items():
            if int(pid) > 0:
                out[page['title']] = page.get('length') or 0
    return out


def main():
    con = sqlite3.connect(DB)
    known = set()
    for table, col in (('Mission', 'TitleEN'), ('CombatSite', 'TitleEN'),
                       ('IncursionSite', 'TitleEN')):
        known |= {r[0].strip().lower() for r in con.execute('select %s from %s' % (col, table))
                  if r[0]}
    print('库里已有标题 %d 个\n' % len(known))

    rows = []
    for cat, label in CATEGORIES:
        titles = members(cat)
        if not titles:
            continue
        sz = sizes(titles)
        total_kb = sum(sz.values()) / 1024
        miss = [t for t in titles if t.lower() not in known]
        miss_kb = sum(sz.get(t, 0) for t in miss) / 1024
        rows.append({'cat': cat, 'label': label, 'pages': len(titles), 'kb': total_kb,
                     'miss': len(miss), 'miss_kb': miss_kb,
                     'samples': [t for t in miss[:6]]})

    rows.sort(key=lambda r: -r['miss_kb'])
    print('%-28s %-18s %5s %9s %6s %9s' % ('分类', '用途', '页数', '总KB', '缺口', '缺口KB'))
    for r in rows:
        print('%-28s %-18s %5d %9.0f %6d %9.0f' % (r['cat'][:28], r['label'][:18],
                                                   r['pages'], r['kb'], r['miss'], r['miss_kb']))
    print('\n=== 缺口明细（前 400KB 的分类）===')
    for r in rows:
        if r['miss_kb'] < 20:
            continue
        print('\n%s（%s）：缺 %d 页 / %.0f KB' % (r['cat'], r['label'], r['miss'], r['miss_kb']))
        print('   ' + '、'.join(r['samples'][:6]))

    json.dump(rows, io.open(os.path.join(HERE, 'gap_report.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('\n→ gap_report.json')


if __name__ == '__main__':
    sys.exit(main())
