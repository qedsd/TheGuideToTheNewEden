# -*- coding: utf-8 -*-
"""批量导入"剩下的站点类攻略"到 `CombatSite`。

覆盖（括号里是 CombatSite 的新分类值）：
  远征 Expeditions (2)        补齐 18 个
  战斗遗迹 Relic sites (5)     7 个（Sleeper Cache 等）
  数据站点 Data sites (10)     20 个（Abandoned Research Complex …）
  矿石站点 Ore sites (11)      66 个
  气云站点 Gas sites (12)      69 个
  冰带 Ice belts (13)          7 个
  虫洞站点 Wormhole sites (14) 76 个

用法：
    python build_extra_sites.py --fetch [组名…]
    python build_extra_sites.py [组名…]
    python build_extra_sites.py --export
    python build_extra_sites.py --merge
    python build_extra_sites.py --write-db [组名…]
组名：expedition / relic / data / ore / gas / ice / wormhole（缺省=全部）
"""

import glob
import hashlib
import io
import json
import os
import re
import sqlite3
import sys
import time
import urllib.parse
import urllib.request

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'missions'))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parse_missions as P
import parse_incursions as I

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
RAW = os.path.join(HERE, 'raw', 'extra_sites')
DB = os.path.join(ROOT, 'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF',
                  'Resources', 'Database', 'static.db')
TRANS_FILE = os.path.join(HERE, 'translations_extra_sites.json')
BATCH_DIR = os.path.join(HERE, 'to_translate5')
OUT_JSON = os.path.join(HERE, 'extra_sites.json')
API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}
SYNC = '2026-11-18'

# 组 → (wiki 分类, CombatSite 分类, 中文类型名)
GROUPS = {
    'expedition': ('Expeditions', 2, '远征'),
    'relic': ('Relic sites', 5, '战斗遗迹'),
    'data': ('Data sites', 10, '数据站点'),
    'ore': ('Ore sites', 11, '矿石站点'),
    'gas': ('Gas sites', 12, '气云站点'),
    'ice': ('Ice belts', 13, '冰带'),
    'wormhole': ('Wormhole sites', 14, '虫洞站点'),
}
FACTION_CN = {
    'rogue drones': '无人机', 'serpentis': '天蛇', 'blood raiders': '血袭者',
    "sansha's nation": '萨沙国度', 'angel cartel': '天使', 'guristas': '古斯塔斯',
    'amarr empire': '艾玛帝国', 'caldari state': '加达里合众国',
    'gallente federation': '盖伦特联邦', 'minmatar republic': '米玛塔尔共和国',
    'concord': '统合部', 'ammatar mandate': '艾玛达', 'sleepers': '冬眠者',
    'drifters': '漂泊者', 'triglavian collective': '三神裔',
}
CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
TRANS = {}


def key(t):
    return hashlib.sha1(t.encode('utf-8')).hexdigest()[:16]


def tr(t):
    if not t:
        return ''
    return TRANS.get(key(t.strip())) or t


def clean_meta(value):
    s = value or ''
    for _ in range(6):
        new = re.sub(r'\{\{[^{}]*\}\}', '', s)
        if new == s:
            break
        s = new
    s = re.sub(r'\[\[(?:[^\]|]*\|)?([^\]]+)\]\]', r'\1', s)
    s = re.sub(r'\[https?://\S+\s+([^\]]+)\]', r'\1', s)
    s = re.sub(r'\[https?://\S+\]', '', s)
    s = re.sub(r'\(\s*\)', '', s)
    s = re.sub(r'<[^>]+>', '', s)
    s = re.sub(r'\s*/\s*(?=/|$)', '', s)
    s = re.sub(r'\s*\|\s*', ' ', s)
    s = re.sub(r'\s+', ' ', s).strip(' |/')
    return '' if s in ('?', '-') else s


def get_json(params):
    url = API + '?' + urllib.parse.urlencode(params)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read().decode('utf-8'))


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


def slug(title):
    return re.sub(r'[^\w\u4e00-\u9fff]+', '_', title)


def fetch(groups):
    os.makedirs(RAW, exist_ok=True)
    for g in groups:
        cat = GROUPS[g][0]
        titles = [t for t in members(cat) if not t.startswith('User:')]
        index = {}
        for t in titles:
            fn = '%s__%s.wiki' % (g, slug(t))
            index[fn] = t
            path = os.path.join(RAW, fn)
            if os.path.exists(path):
                continue
            url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
                {'title': t.replace(' ', '_'), 'action': 'raw'})
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers=UA),
                                            timeout=60) as r:
                    text = r.read().decode('utf-8', 'replace')
            except Exception as ex:  # noqa: BLE001
                print('  FAIL', t, ex)
                continue
            io.open(path, 'w', encoding='utf-8').write(text)
            time.sleep(0.25)
        json.dump(index, io.open(os.path.join(RAW, 'index_%s.json' % g), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)
        print('%-10s %-18s %d 页' % (g, cat, len(index)))


HTML_RAW = os.path.join(HERE, 'raw', 'extra_sites_html')


def fetch_html(groups):
    """虫洞站点页的内容几乎全在 `{{npcwh|…}}` 模板里（波次 + 舰型代码），
    wikitext 解析不出东西 → 改抓**渲染后的 HTML**，模板已展开成表格。"""
    os.makedirs(HTML_RAW, exist_ok=True)
    for g in groups:
        idx_path = os.path.join(RAW, 'index_%s.json' % g)
        title_of = json.load(io.open(idx_path, encoding='utf-8'))
        for title in title_of.values():
            path = os.path.join(HTML_RAW, '%s__%s.html' % (g, slug(title)))
            if os.path.exists(path):
                continue
            url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
                {'title': title, 'action': 'render'})
            try:
                with urllib.request.urlopen(urllib.request.Request(url, headers=UA),
                                            timeout=60) as r:
                    html = r.read().decode('utf-8', 'replace')
            except Exception as ex:  # noqa: BLE001
                print('  FAIL', title, ex)
                continue
            io.open(path, 'w', encoding='utf-8').write(html)
            time.sleep(0.25)
        print('%-10s HTML %d 页' % (g, len(title_of)))


def html_sections(html):
    import lxml.html
    doc = lxml.html.fromstring(html)
    node = doc.xpath('//*[@id="mw-content-text"]')
    node = node[0] if node else doc
    I.clean_node(node)
    items = []
    I.walk(node, items)
    sections, cur = [], {'title': '', 'text': []}
    for kind, lv, text in items:
        if kind == 'heading':
            if cur['title'] or cur['text']:
                sections.append(cur)
            cur = {'title': text, 'text': []}
        else:
            cur['text'].append(text)
    if cur['title'] or cur['text']:
        sections.append(cur)
    for s in sections:
        s['text'] = '\n\n'.join(s['text'])
    return sections


def build_html_group(g, seen_titles=None):
    _cat, code, label = GROUPS[g]
    seen_titles = seen_titles if seen_titles is not None else set()
    idx_path = os.path.join(RAW, 'index_%s.json' % g)
    title_of = json.load(io.open(idx_path, encoding='utf-8'))
    items = []
    for title in sorted(title_of.values()):
        path = os.path.join(HTML_RAW, '%s__%s.html' % (g, slug(title)))
        if not os.path.exists(path):
            continue
        sections = html_sections(io.open(path, encoding='utf-8').read())
        parts = ['**分类** %s' % label, '']
        for sec in sections:
            if sec['title']:
                parts.append('### ' + tr(sec['title']))
                parts.append('')
            if sec['text']:
                parts.append(tr(sec['text']))
                parts.append('')
        content = '\n'.join(parts).strip()
        content += '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC
        items.append({'group': g, 'Category': code, 'TitleEN': title, 'TitleCN': tr(title),
                      'Rating': '', 'Location': '', 'ShipLimit': '', 'WikiContent': content})
    return items


def npc_md(tables):
    out = []
    for tb in tables:
        title = (tb.get('title') or '').lstrip('|').strip()
        if title:
            out.append('**%s**' % tr(title))
            out.append('')
        out.append('| 舰型 | 数量 | 名称 | 备注 |')
        out.append('|---|---|---|---|')
        for r in tb.get('rows') or []:
            note = '；'.join(x for x in [tr(r.get('trigger') or ''), tr(r.get('note') or '')] if x)
            out.append('| %s | %s | %s | %s |' % (r.get('cls') or '', r.get('count') or '',
                                                  tr(r.get('name') or ''), note))
        out.append('')
    return '\n'.join(out)


def infobox(text):
    for name in ('CMBSiteInfo', 'Template:CMBSiteInfo', 'Wormhole Infobox', 'Template:Wormhole Infobox'):
        box = P.find_templates(text, {name})
        if box:
            return P.parse_info(box[0][3])
    return {}


def build(groups):
    items = []
    seen_titles = set()
    for g in groups:
        if g in ('wormhole',):
            items.extend(build_html_group(g, seen_titles))
            continue
        idx_path = os.path.join(RAW, 'index_%s.json' % g)
        title_of = json.load(io.open(idx_path, encoding='utf-8')) if os.path.exists(idx_path) else {}
        _cat, code, label = GROUPS[g]
        for fn, title in sorted(title_of.items()):
            path = os.path.join(RAW, fn)
            if not os.path.exists(path):
                continue
            text = io.open(path, encoding='utf-8').read()
            info = infobox(text)
            name = (info.get('name') or '').strip() or title
            body = re.sub(r'(?is)<noinclude>.*?</noinclude>', '', text)
            body = re.sub(r'(?is)<includeonly>.*?</includeonly>', '', body)
            box = P.find_templates(body, {'CMBSiteInfo', 'Template:CMBSiteInfo',
                                          'Wormhole Infobox', 'Template:Wormhole Infobox'})
            if box:
                body = body[:box[0][0]] + body[box[0][1]:]
            npcs, body_wo = P.collect_npcs(body)
            sections = []
            for lvl, st, sb in P.split_sections(body_wo):
                clean = P.paragraphize(sb)
                if not clean and not st:
                    continue
                sections.append({'title': P.strip_wiki_markup(st), 'text': clean})
            if npcs:
                sections.append({'title': '', 'text': npc_md(npcs)})

            meta = ['**分类** %s' % label]
            for fld, cn in (('rating', '评级'), ('location', '区域'), ('faction', '势力'),
                            ('ship limit', '船型限制'), ('signature strength', '信号强度')):
                v = clean_meta(info.get(fld))
                if v:
                    meta.append('**%s** %s' % (cn, FACTION_CN.get(v.lower(), v)))
            parts = [' ｜ '.join(meta), '']
            for sec in sections:
                if sec['title']:
                    parts.append('### ' + tr(sec['title']))
                    parts.append('')
                if sec['text']:
                    parts.append(tr(sec['text']))
                    parts.append('')
            content = '\n'.join(parts).strip()
            content += '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC
            if name.lower() in seen_titles:
                continue
            seen_titles.add(name.lower())
            items.append({'group': g, 'Category': code, 'TitleEN': name, 'TitleCN': tr(name),
                          'Rating': clean_meta(info.get('rating')),
                          'Location': clean_meta(info.get('location')),
                          'ShipLimit': clean_meta(info.get('ship limit')),
                          'WikiContent': content})
    return items


def export():
    items = build(list(GROUPS))
    seen = {}
    for it in items:
        for line in [it['TitleEN']] + it['WikiContent'].split('\n'):
            s = line.strip()
            if s.startswith('### '):
                s = s[4:].strip()
            if s.startswith('|'):
                for cell in s.split('|'):
                    c = cell.strip()
                    if c and not CJK.search(c) and len(LATIN.findall(c)) >= 1 and key(c) not in TRANS:
                        seen[c] = True
                continue
            if not s or s.startswith('>') or CJK.search(s) or key(s) in TRANS:
                continue
            if len(LATIN.findall(s)) < 2 and len(s.split()) > 2:
                continue
            seen[s] = True
    entries = sorted(seen)
    os.makedirs(BATCH_DIR, exist_ok=True)
    for old in os.listdir(BATCH_DIR):
        if old.startswith('batch_') or old.startswith('zh_'):
            os.remove(os.path.join(BATCH_DIR, old))
    batch, size, idx = [], 0, 0
    for text in entries:
        if batch and size + len(text) > 9000:
            json.dump([{'k': key(t), 'en': t} for t in batch],
                      io.open(os.path.join(BATCH_DIR, 'batch_%03d.json' % idx), 'w', encoding='utf-8'),
                      ensure_ascii=False, indent=1)
            idx += 1
            batch, size = [], 0
        batch.append(text)
        size += len(text)
    if batch:
        json.dump([{'k': key(t), 'en': t} for t in batch],
                  io.open(os.path.join(BATCH_DIR, 'batch_%03d.json' % idx), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)
        idx += 1
    print('待翻译 %d 条 / %d 字符 → %d 批（%s）'
          % (len(entries), sum(len(e) for e in entries), idx, BATCH_DIR))


def merge():
    added = 0
    for zh in sorted(glob.glob(os.path.join(BATCH_DIR, 'zh_*.json'))):
        for k, v in json.load(io.open(zh, encoding='utf-8')).items():
            v = (v or '').strip()
            if v and CJK.search(v):
                if k not in TRANS:
                    added += 1
                TRANS[k] = v
    json.dump(TRANS, io.open(TRANS_FILE, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文 %d 条（新增 %d）' % (len(TRANS), added))


def write_db(items, groups):
    codes = [GROUPS[g][1] for g in groups]
    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute('SELECT MAX(Id) FROM CombatSite')
    rid = cur.fetchone()[0] or 0
    rows = []
    for it in items:
        rid += 1
        rows.append((rid, it['Category'], -1, it['Rating'], '', '', -1, it['ShipLimit'],
                     it['TitleCN'], it['TitleEN'], '', 'wiki', it['Location'], 0, it['TitleEN'],
                     '', it['WikiContent'], 0, SYNC, SYNC))
    for code in codes:
        cur.execute('DELETE FROM CombatSite WHERE Category = ?', (code,))
    cur.executemany(
        'INSERT INTO CombatSite (Id,Category,Faction,Rating,Tier,Level,Security,ShipLimit,'
        'TitleCN,TitleEN,Escalation,Source,Location,EscalationId,WikiTitle,CommunityContent,'
        'WikiContent,IsStub,CommunityUpdatedAt,WikiUpdatedAt) '
        'VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)', rows)
    con.commit()
    print('写入 CombatSite %d 条（分类 %s）' % (len(rows), codes))


def main():
    global TRANS
    TRANS = json.load(io.open(TRANS_FILE, encoding='utf-8')) if os.path.exists(TRANS_FILE) else {}
    groups = [a for a in sys.argv[1:] if a in GROUPS] or list(GROUPS)
    if '--fetch' in sys.argv:
        fetch(groups)
        return 0
    if '--fetch-html' in sys.argv:
        fetch_html(groups)
        return 0
    if '--export' in sys.argv:
        export()
        return 0
    if '--merge' in sys.argv:
        merge()
        return 0
    items = build(groups)
    json.dump(items, io.open(OUT_JSON, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    by = {}
    for it in items:
        by[it['group']] = by.get(it['group'], 0) + 1
    print('解析 %d 个站点：%s（共 %d 字符）'
          % (len(items), by, sum(len(i['WikiContent']) for i in items)))
    if '--write-db' in sys.argv:
        write_db(items, groups)
    return 0


if __name__ == '__main__':
    sys.exit(main())
