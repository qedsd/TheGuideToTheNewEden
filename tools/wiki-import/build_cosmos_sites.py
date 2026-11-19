# -*- coding: utf-8 -*-
"""导入 COSMOS 站点（Category:COSMOS sites，87 页）到 `CombatSite` 表。

COSMOS 站点和 DED 复合体是同一类东西（`{{CMBSiteInfo}}` 信息框 + NPC 表 + 攻略正文），
所以直接进 `CombatSite`，用新的分类值 **9 = COSMOS**，在「死亡远征」页的分类里能筛到。

用法：
    python build_cosmos_sites.py --fetch      # 抓 raw（缓存在 raw/cosmos_sites/）
    python build_cosmos_sites.py              # 解析并生成 cosmos_sites.json
    python build_cosmos_sites.py --export     # 导出待翻译片段
    python build_cosmos_sites.py --merge      # 合并译文
    python build_cosmos_sites.py --write-db   # 写入 static.db 的 CombatSite
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
import parse_missions as P

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
RAW = os.path.join(HERE, 'raw', 'cosmos_sites')
STATIC_DB = os.path.join(ROOT, 'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF',
                         'Resources', 'Database', 'static.db')
TRANS_FILE = os.path.join(HERE, 'translations_cosmos_sites.json')
BATCH_DIR = os.path.join(HERE, 'to_translate4')
OUT_JSON = os.path.join(HERE, 'cosmos_sites.json')
API = 'https://wiki.eveuniversity.org/api.php'
UA = {'User-Agent': 'TheGuideToTheNewEden/1.0'}
SYNC = '2026-11-18'

CATEGORY_COSMOS = 9
FACTION_MAP = {'rogue drone': 0, 'drone': 0, 'serpentis': 1, 'blood raider': 2,
               'sansha': 3, 'angel': 4, 'gurista': 5}
FACTION_CN = {
    'rogue drones': '无人机', 'rogue drone': '无人机',
    'serpentis': '天蛇', 'blood raiders': '血袭者', 'blood raider': '血袭者',
    "sansha's nation": '萨沙国度', 'sansha': '萨沙国度',
    'angel cartel': '天使', 'guristas': '古斯塔斯',
    'amarr empire': '艾玛帝国', 'caldari state': '加达里合众国',
    'gallente federation': '盖伦特联邦', 'minmatar republic': '米玛塔尔共和国',
    'amarr': '艾玛帝国', 'caldari': '加达里合众国',
    'gallente': '盖伦特联邦', 'minmatar': '米玛塔尔共和国',
    'concord': '统合部', 'ammatar mandate': '艾玛达',
}
SECURITY_MAP = {'high': 0, 'low': 1, 'null': 2}
TYPE_CN = {'cosmos': '空中代理人站点', 'ded': 'DED 复合体', 'anomaly': '异常'}
CJK = re.compile(r'[\u4e00-\u9fff]')
LATIN = re.compile(r'[A-Za-z]{2,}')
TRANS = {}


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def tr(text):
    if not text:
        return ''
    t = text.strip()
    return TRANS.get(key(t)) or t


def tr_block(text):
    out = []
    for line in (text or '').split('\n'):
        s = line.strip()
        if not s:
            out.append('')
            continue
        if s.startswith('|---'):
            out.append(s)
            continue
        if s.startswith('|'):
            cells = s.split('|')
            out.append('|'.join(tr(c.strip()) if c.strip() and not CJK.search(c) else c
                                for c in cells))
            continue
        if CJK.search(s):
            out.append(line.rstrip())
            continue
        out.append(tr(s))
    return '\n'.join(out).strip()


def get_json(params):
    url = API + '?' + urllib.parse.urlencode(params)
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read().decode('utf-8'))


def members(cat, limit=1000):
    out, cont = [], None
    while True:
        params = {'action': 'query', 'list': 'categorymembers', 'cmtitle': 'Category:' + cat,
                  'cmlimit': '500', 'cmtype': 'page', 'format': 'json'}
        if cont:
            params['cmcontinue'] = cont
        data = get_json(params)
        out += [m['title'] for m in data.get('query', {}).get('categorymembers', [])]
        cont = data.get('continue', {}).get('cmcontinue')
        if not cont or len(out) >= limit:
            break
    return out[:limit]


def fetch():
    os.makedirs(RAW, exist_ok=True)
    titles = members('COSMOS sites')
    print('COSMOS 站点 %d 页' % len(titles))
    for t in titles:
        path = os.path.join(RAW, re.sub(r'[^\w\u4e00-\u9fff]+', '_', t) + '.wiki')
        if os.path.exists(path):
            continue
        url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
            {'title': t.replace(' ', '_'), 'action': 'raw'})
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
                text = r.read().decode('utf-8', 'replace')
        except Exception as ex:  # noqa: BLE001
            print('  FAIL', t, ex)
            continue
        io.open(path, 'w', encoding='utf-8').write(text)
        time.sleep(0.3)
    index = {}
    for t in titles:
        index[re.sub(r'[^\w\u4e00-\u9fff]+', '_', t) + '.wiki'] = t
    json.dump(index, io.open(os.path.join(RAW, 'index.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('抓取完成，%d 个文件' % len(glob.glob(os.path.join(RAW, '*.wiki'))))


def clean_meta(value):
    """信息框字段里常见 `{{ColorSecurityRating|{{SystemToSecurity|Otitoh}} }}`、
    `[[链接|文本]]` 这类 wiki 语法，清成纯文本。"""
    s = value or ''
    for _ in range(6):
        new = re.sub(r'\{\{[^{}]*\}\}', '', s)
        if new == s:
            break
        s = new
    s = re.sub(r'\[\[(?:[^\]|]*\|)?([^\]]+)\]\]', r'\1', s)
    # 外链 `[https://evemaps... Kenobalana]` → Kenobalana
    s = re.sub(r'\[https?://\S+\s+([^\]]+)\]', r'\1', s)
    s = re.sub(r'\[https?://\S+\]', '', s)
    s = re.sub(r'\(\s*\)', '', s)
    s = re.sub(r'<[^>]+>', '', s)
    s = re.sub(r'\s+/\s*(?=/|$)', '', s)     # 去掉清理后残留的空 ` / `
    s = re.sub(r'\s*\|\s*', ' ', s)
    s = re.sub(r'\s+', ' ', s).strip(' |/')
    return '' if s in ('?', '-') else s


def infobox(text):
    for name in ('CMBSiteInfo', 'Template:CMBSiteInfo'):
        box = P.find_templates(text, {name})
        if box:
            _s, _e, _n, body = box[0]
            return P.parse_info(body)
    return {}


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


def build():
    items = []
    idx_path = os.path.join(RAW, 'index.json')
    title_of = json.load(io.open(idx_path, encoding='utf-8')) if os.path.exists(idx_path) else {}
    for path in sorted(glob.glob(os.path.join(RAW, '*.wiki'))):
        text = io.open(path, encoding='utf-8').read()
        info = infobox(text)
        base = os.path.basename(path)
        name = (info.get('name') or '').strip() or title_of.get(base) or base[:-5]
        # 没有 CMBSiteInfo 的（地标/据点类）也收进来，只是属性区为空
        body = re.sub(r'(?is)<noinclude>.*?</noinclude>', '', text)
        body = re.sub(r'(?is)<includeonly>.*?</includeonly>', '', body)
        # 去掉信息框本身
        box = P.find_templates(body, {'CMBSiteInfo', 'Template:CMBSiteInfo'})
        if box:
            s, e = box[0][0], box[0][1]
            body = body[:s] + body[e:]
        npcs, body_wo = P.collect_npcs(body)

        sections = []
        for lvl, stitle, sbody in P.split_sections(body_wo):
            clean = P.paragraphize(sbody)
            if not clean and not stitle:
                continue
            sections.append({'level': lvl, 'title': P.strip_wiki_markup(stitle), 'text': clean})
        if npcs:
            sections.append({'level': 0, 'title': '', 'text': npc_md(npcs)})

        meta = ['**分类** %s' % TYPE_CN.get((info.get('type') or 'cosmos').lower(), '空中代理人站点')]
        if info.get('rating'):
            meta.append('**评级** %s' % clean_meta(info.get('rating')))
        if info.get('location'):
            meta.append('**区域** %s' % clean_meta(info.get('location')))
        if info.get('ship limit'):
            meta.append('**船型限制** %s' % clean_meta(info.get('ship limit')))
        fac = clean_meta(info.get('faction'))
        if fac:
            meta.append('**势力** %s' % FACTION_CN.get(fac.lower(), fac))
        if info.get('signature strength'):
            meta.append('**信号强度** %s' % clean_meta(info.get('signature strength')))

        parts = [' ｜ '.join(meta), '']
        for sec in sections:
            if sec['title']:
                parts.append('### ' + tr(sec['title']))
                parts.append('')
            if sec['text']:
                parts.append(tr_block(sec['text']))
                parts.append('')
        content = '\n'.join(parts).strip()
        content += '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC

        faction = -1
        for k, v in FACTION_MAP.items():
            if k in (info.get('faction') or '').lower():
                faction = v
                break
        security = SECURITY_MAP.get((info.get('location') or '').split(',')[0].strip().lower(), -1)
        items.append({
            'TitleEN': name,
            'TitleCN': tr(name),
            'Faction': faction,
            'Security': security,
            'ShipLimit': clean_meta(info.get('ship limit')),
            'Rating': clean_meta(info.get('rating')),
            'Location': clean_meta(info.get('location')),
            'WikiContent': content,
            'Sections': len(sections),
            'chars': len(content),
        })
    return items


def export():
    items = build()
    seen = {}
    for it in items:
        for text in [it['TitleEN']] + [line.strip() for line in it['WikiContent'].split('\n')]:
            s = text
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
    print('待翻译 %d 条 / %d 字符 → %d 批' % (len(entries), sum(len(e) for e in entries), idx))


def merge():
    added = 0
    for zh in sorted(glob.glob(os.path.join(BATCH_DIR, 'zh_*.json'))):
        data = json.load(io.open(zh, encoding='utf-8'))
        for k, v in data.items():
            v = (v or '').strip()
            if v and CJK.search(v):
                if k not in TRANS:
                    added += 1
                TRANS[k] = v
    json.dump(TRANS, io.open(TRANS_FILE, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('译文 %d 条（新增 %d）' % (len(TRANS), added))


def write_db(items):
    con = sqlite3.connect(STATIC_DB)
    cur = con.cursor()
    cur.execute('DELETE FROM CombatSite WHERE Category = ?', (CATEGORY_COSMOS,))
    cur.execute('SELECT MAX(Id) FROM CombatSite')
    rid = (cur.fetchone()[0] or 0)
    rows = []
    for it in items:
        rid += 1
        rows.append((rid, CATEGORY_COSMOS, it['Faction'], it['Rating'], '', '', it['Security'],
                     it['ShipLimit'], it['TitleCN'], it['TitleEN'], '', 'wiki', it['Location'],
                     0, it['TitleEN'], '', it['WikiContent'], 0, SYNC, SYNC))
    cur.executemany(
        'INSERT INTO CombatSite (Id,Category,Faction,Rating,Tier,Level,Security,ShipLimit,'
        'TitleCN,TitleEN,Escalation,Source,Location,EscalationId,WikiTitle,CommunityContent,'
        'WikiContent,IsStub,CommunityUpdatedAt,WikiUpdatedAt) '
        'VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)', rows)
    con.commit()
    print('写入 CombatSite %d 条（Category=%d）' % (len(rows), CATEGORY_COSMOS))


def main():
    global TRANS
    TRANS = json.load(io.open(TRANS_FILE, encoding='utf-8')) if os.path.exists(TRANS_FILE) else {}
    if '--fetch' in sys.argv:
        fetch()
        return 0
    if '--export' in sys.argv:
        export()
        return 0
    if '--merge' in sys.argv:
        merge()
        return 0
    items = build()
    json.dump(items, io.open(OUT_JSON, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('解析 %d 个 COSMOS 站点，共 %d 字符' % (len(items), sum(i['chars'] for i in items)))
    for it in items[:5]:
        print('  %-42s %-10s %s' % (it['TitleCN'][:42], it['Rating'] or '-', it['Location'] or '-'))
    if '--write-db' in sys.argv:
        write_db(items)
    return 0


if __name__ == '__main__':
    sys.exit(main())
