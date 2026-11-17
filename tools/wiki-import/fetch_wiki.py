"""EVE University Wiki「Combat site」抓取器。

用途
----
把 <https://wiki.eveuniversity.org/Combat_site> 页面上列出的**全部站点**逐条抓到本地，
产出可离线复现的原始数据集，供 `build_sites.py` 解析、翻译并写入数据库。

产物（都在本目录下）
--------------------
- `raw/combat_site.html` —— 索引页渲染后的 HTML（用它取表格与真实链接目标）
- `raw/pages/<Page_Title>.wiki` —— 每个站点子页的 wikitext（`action=raw`）
- `index.json` —— 分类 → [{site, label, page}] 的索引

用法
----
    python fetch_wiki.py            # 增量：已存在的页面跳过
    python fetch_wiki.py --force    # 全量重抓

只依赖标准库；限速 0.3s/请求，避免给 wiki 添麻烦。
"""

import io
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

import lxml.html

BASE = 'https://wiki.eveuniversity.org'
API = BASE + '/api.php'
HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
PAGES = os.path.join(RAW, 'pages')
UA = 'TheGuideToTheNewEden-wiki-import/1.0 (+https://github.com/TheGuideToTheNewEden)'
DELAY = 0.3

FACT6 = ['Angel Cartel', 'Blood Raiders', 'Guristas Pirates', "Sansha's Nation",
         'Serpentis Corporation', 'Rogue Drones']


def get(url, binary=False):
    req = urllib.request.Request(url, headers={'User-Agent': UA})
    with urllib.request.urlopen(req, timeout=90) as resp:
        data = resp.read()
    return data if binary else data.decode('utf-8', 'replace')


def grid(tbl):
    """把 HTML 表格展开成矩形网格（正确处理 rowspan / colspan），每个格子保留 (文本, 链接目标)。"""
    rows = tbl.xpath('./tr | ./thead/tr | ./tbody/tr')
    occupied, out = {}, []
    for ri, tr in enumerate(rows):
        ci = 0
        for cell in tr.xpath('./th | ./td'):
            while (ri, ci) in occupied:
                ci += 1
            cs = int(cell.get('colspan') or 1)
            rs = int(cell.get('rowspan') or 1)
            txt = ' '.join(' '.join(cell.itertext()).split())
            a = cell.xpath('.//a/@href')
            page = ''
            for h in a:
                if h.startswith('/') and not h.startswith(('/index.php', '/Special:', '/Category:', '/File:', '/Help:')):
                    page = urllib.parse.unquote(h[1:])
                    break
            for dr in range(rs):
                for dc in range(cs):
                    occupied[(ri + dr, ci + dc)] = (txt, page)
            ci += cs
        if ri in [k[0] for k in occupied]:
            w = max(c for (r, c) in occupied if r == ri) + 1
            out.append([occupied.get((ri, c), ('', '')) for c in range(w)])
    w = max(len(r) for r in out)
    for r in out:
        r.extend([('', '')] * (w - len(r)))
    return out


def clean(x):
    return re.sub(r'\s*\[\s*\d+\s*\]', '', x or '').strip()


def safe_name(title):
    """Windows 文件名不允许 ? : * " < > | —— 换成等价字符，其余保留（解析时用 index.json 反查回原标题）。"""
    table = {'?': '？', ':': '：', '*': '＊', '"': '＂', '<': '＜', '>': '＞', '|': '｜', '/': '／'}
    for bad, good in table.items():
        title = title.replace(bad, good)
    return title


def build_index(html):
    doc = lxml.html.fromstring(html)
    tables = doc.xpath('//table[contains(@class,"wikitable")]')
    idx = {'anomalies': [], 'triglavian': [], 'unrated': [], 'dedRated': [],
           'chemicalLabs': [], 'expeditions': []}

    def push(bucket, faction, site, page, **extra):
        site = clean(site)
        if not site or site in ('None', '-', 'N/A'):
            return
        item = {'faction': faction, 'site': site, 'page': page}
        item.update(extra)
        if not any(x['site'] == site and x['faction'] == faction for x in idx[bucket]):
            idx[bucket].append(item)

    # 0 战斗异常：Tier / Level / High / Low / Null / 6 势力 × (站点, 升级目标)
    for row in grid(tables[0])[2:]:
        tier, level = clean(row[0][0]), clean(row[1][0])
        if not tier:
            continue
        for i, f in enumerate(FACT6):
            site, page = row[5 + i * 2]
            esc, escpage = row[6 + i * 2]
            push('anomalies', f, site, page, tier='T' + tier, level=level,
                 high=bool(clean(row[2][0])), low=bool(clean(row[3][0])), null=bool(clean(row[4][0])),
                 escalation=clean(esc) if clean(esc) not in ('None', '-', 'N/A') else '',
                 escalationPage=escpage)
    # 1 三神裔
    gt = grid(tables[1])
    for row in gt[1:]:
        for c in range(1, len(row)):
            push('triglavian', clean(row[c][0]), row[c][0], row[c][1],
                 side=clean(row[0][0]), cls=clean(gt[0][c][0]))
    # 2 无评级复合体
    for row in grid(tables[2])[2:]:
        for i, f in enumerate(FACT6):
            push('unrated', f, row[3 + i][0], row[3 + i][1],
                 high=bool(clean(row[0][0])), low=bool(clean(row[1][0])), null=bool(clean(row[2][0])))
    # 4 DED 评级
    for row in grid(tables[4])[2:]:
        rating = clean(row[0][0])
        if not re.match(r'^\d+/10$', rating):
            continue
        for i, f in enumerate(FACT6):
            push('dedRated', f, row[4 + i][0], row[4 + i][1], rating=rating,
                 high=bool(clean(row[1][0])), low=bool(clean(row[2][0])), null=bool(clean(row[3][0])))
    # 5 化学实验室
    cur = ''
    for row in grid(tables[5])[1:]:
        if clean(row[0][0]):
            cur = clean(row[0][0])
        push('chemicalLabs', cur, row[1][0], row[1][1], location=clean(row[2][0]))
    # 6 远征
    src = ''
    for row in grid(tables[6])[2:]:
        head = clean(row[0][0])
        if head in ('High Sec', 'Low Sec', 'Null Sec'):
            src = head
            continue
        if not head:
            continue
        src = head
        for i, f in enumerate(FACT6):
            push('expeditions', f, row[1 + i][0], row[1 + i][1], source=src)
    return idx


EXTRA_PAGES = {
    'relicSites': ['Ancient_Ruins', 'Bloated_Ruins', 'Festering_Ruins', 'Forgotten_Ruins',
                   'Hidden_Ruins', 'Wispy_Ruins'],
    'special': ['ISHAEKA_Tactical_Response_HQ', 'Blood_Raider_Temple_Complex',
                'Blood_Raider_Temple_Complex_(The_Inner_Sanctum)', 'Besieged_Covert_Research_Factory'],
}


def main():
    force = '--force' in sys.argv
    os.makedirs(PAGES, exist_ok=True)

    index_path = os.path.join(HERE, 'index.json')
    if os.path.exists(index_path):
        idx = json.load(io.open(index_path, encoding='utf-8'))
        print('index.json 已存在，复用（--force 时重抓索引页）')
        if force:
            idx = None
    else:
        idx = None

    if idx is None:
        print('抓索引页 Combat_site …')
        html = get(API + '?action=parse&page=Combat_site&prop=text&format=json&formatversion=2')
        payload = json.loads(html)
        html = payload['parse']['text']
        io.open(os.path.join(RAW, 'combat_site.html'), 'w', encoding='utf-8').write(html)
        idx = build_index(html)
        meta = {'source': BASE + '/Combat_site', 'fetchedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime())}
        try:
            rev = json.loads(get(API + '?action=query&prop=revisions&titles=Combat_site'
                                 '&rvprop=timestamp|ids&rvlimit=1&format=json&formatversion=2'))
            r = rev['query']['pages'][0]['revisions'][0]
            meta['revision'] = r['revid']
            meta['revisionTimestamp'] = r['timestamp']
        except Exception as exc:  # 拿不到版本号不影响主流程
            print('  取版本号失败：', exc)
        for bucket, titles in EXTRA_PAGES.items():
            idx[bucket] = [{'faction': '', 'site': t.replace('_', ' '), 'page': t} for t in titles]
        idx['meta'] = meta
        json.dump(idx, io.open(index_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        counts = {k: len(v) for k, v in idx.items() if isinstance(v, list)}
        print('索引完成：', counts)

    pages = {}
    for bucket, items in idx.items():
        if not isinstance(items, list):
            continue
        for it in items:
            if it['page']:
                pages.setdefault(it['page'], bucket)

    done = skipped = failed = 0
    for title, bucket in sorted(pages.items()):
        path = os.path.join(PAGES, safe_name(title) + '.wiki')
        if os.path.exists(path) and not force:
            skipped += 1
            continue
        url = BASE + '/index.php?title=' + urllib.parse.quote(title) + '&action=raw'
        try:
            text = get(url)
        except Exception as exc:
            print('  [失败] %s：%s' % (title, exc))
            failed += 1
            continue
        if text.lstrip().startswith('#REDIRECT'):
            m = re.search(r'\[\[([^\]\|#]+)', text)
            if m:
                target = m.group(1).strip()
                try:
                    text = get(BASE + '/index.php?title=' + urllib.parse.quote(target) + '&action=raw')
                    title = target
                    path = os.path.join(PAGES, safe_name(title) + '.wiki')
                except Exception:
                    pass
        io.open(path, 'w', encoding='utf-8').write(text)
        done += 1
        if done % 25 == 0:
            print('  已抓 %d …' % done)
        time.sleep(DELAY)

    print('抓取完成：新增 %d，跳过 %d（已存在），失败 %d，去重后页面总数 %d'
          % (done, skipped, failed, len(pages)))


if __name__ == '__main__':
    main()


