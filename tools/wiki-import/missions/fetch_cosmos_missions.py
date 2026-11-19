# -*- coding: utf-8 -*-
"""补抓「COSMOS 任务」里**没有 {{Missiondetails}} 信息框**的那批页面。

样例（`A Storm Brewing (1 of 6)`）：
    <noinclude> [[Gallente COSMOS]] mission<br> Agent: [[Preaux Gallot]]<br>
      Agent location: Natural Seminary (Audaerne)<br>
      Standings required: 1.00 Gallente Federation<br> Parts: ... </noinclude>
    {{MissionBriefing|…}}
    {{NPCTableHead}}…{{NPCTableRow}}…

这些页的元信息在开头那一行 `Agent: / Agent location: / Standings required:` 里，
正文是简报 + NPC 表。产出 `cosmos_extra.json`，由 build_missions.py 追加进 Mission 表。
"""

import io
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

import parse_missions as P

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
OUT = os.path.join(HERE, 'cosmos_extra.json')
API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}
DELAY = 0.35


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


def raw(title):
    url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
        {'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def parse_meta(text):
    """开头那一行的 `Agent: / Agent location: / Standings required:` → 字典。"""
    head = text[:1200]
    out = {}
    for key, field in (('Agent', 'agent'), ('Agent location', 'location'),
                       ('Standings required', 'standing'), ('Parts', 'parts'),
                       ('Faction', 'faction')):
        m = re.search(r'(?i)%s\s*:\s*(?:\[\[)?([^<\n\]]+)' % re.escape(key), head)
        if m:
            out[field] = P.strip_wiki_markup(m.group(1)).strip(' |')
    m = re.search(r'\[\[([^\]]*COSMOS)\]\]', head)
    if m:
        out['cosmos'] = P.strip_wiki_markup(m.group(1)).strip()
    return out


def parse(text):
    """页面 → {meta, info, briefing, sections}。"""
    meta = parse_meta(text)
    body = re.sub(r'(?is)<noinclude>.*?</noinclude>', '', text)
    body = re.sub(r'(?is)<includeonly>.*?</includeonly>', '', body)

    briefing = ''
    briefs = P.find_templates(body, {'MissionBriefing'})
    if briefs:
        b_start, b_end, _bn, b_body = briefs[0]
        briefing = P.paragraphize(b_body.lstrip('|').strip()).strip('|').strip()
        body = body[:b_start] + body[b_end:]

    npcs, body_wo = P.collect_npcs(body)
    sections = []
    for lvl, stitle, sbody in P.split_sections(body_wo):
        clean = P.paragraphize(sbody)
        if not clean and not stitle:
            continue
        sections.append({'level': lvl, 'title': P.strip_wiki_markup(stitle), 'text': clean, 'npcs': []})
    if npcs:
        sections.append({'level': 0, 'title': '', 'text': '', 'npcs': npcs})
    return {'meta': meta, 'briefing': briefing, 'sections': sections}


def main():
    # 用法：python fetch_cosmos_missions.py [分类名] [MissionCategory] [输出文件名]
    cat_name = sys.argv[1] if len(sys.argv) > 1 else 'COSMOS missions'
    cat_code = int(sys.argv[2]) if len(sys.argv) > 2 else 6
    out_name = sys.argv[3] if len(sys.argv) > 3 else 'cosmos_extra.json'
    titles = members(cat_name)
    # 以**数据库**为准判断缺哪些（index 里大部分页面早就抓过，只是没有 Missiondetails 没能入库）
    import sqlite3
    db = os.path.abspath(os.path.join(HERE, '..', '..', '..',
                                      'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF',
                                      'Resources', 'Database', 'static.db'))
    con = sqlite3.connect(db)
    known = {r[0].lower() for r in con.execute('select TitleEN from Mission')}
    todo = [t for t in titles if t.lower() not in known]
    print('%s：%d 页，数据库里没有的 %d 个' % (cat_name, len(titles), len(todo)))

    out = []
    for title in todo:
        path = os.path.join(RAW, re.sub(r'[^0-9A-Za-z\u4e00-\u9fff]+', '_', title).strip('_') + '.txt')
        if os.path.exists(path):
            text = io.open(path, encoding='utf-8').read()
        else:
            try:
                text = raw(title)
            except Exception as ex:  # noqa: BLE001
                print('  FAIL %s: %s' % (title, ex))
                continue
            io.open(path, 'w', encoding='utf-8').write(text)
            time.sleep(DELAY)
        parsed = parse(text)
        parsed['cat'] = cat_code
        out.append({'title': title, **parsed})
        print('  %-46s agent=%-18s 节 %d' % (title[:46], (parsed['meta'].get('agent') or '-')[:18],
                                            len(parsed['sections'])))

    out_path = os.path.join(HERE, out_name)
    json.dump(out, io.open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n解析 %d 页 → %s' % (len(out), out_path))


if __name__ == '__main__':
    sys.exit(main())
