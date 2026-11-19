# -*- coding: utf-8 -*-
"""抽样看几类攻略页的"结构强度"：有没有 infobox 模板、正文多长、有多少表格。

结构化程度决定导入成本：模板驱动（k=v）好导，纯散文只能整篇翻译。
"""

import io
import json
import re
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


def raw(title):
    url = 'https://wiki.eveuniversity.org/index.php?' + urllib.parse.urlencode(
        {'title': title.replace(' ', '_'), 'action': 'raw'})
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read().decode('utf-8', 'replace')


def profile(title):
    try:
        text = raw(title)
    except Exception as ex:  # noqa: BLE001
        return {'title': title, 'error': str(ex)}
    templates = re.findall(r'\{\{\s*([A-Za-z0-9_ ]+)', text)
    top = [t.strip() for t in templates]
    sections = len(re.findall(r'(?m)^==+\s*[^=]+\s*==+', text))
    tables = text.count('{{NPCTableHead') + text.count('{|')
    return {
        'title': title,
        'chars': len(text),
        'sections': sections,
        'tables': tables,
        'templates': sorted(set(top))[:8],
        'has_infobox': any(t.lower() in ('infobox', 'anomaly', 'wormhole', 'dedcomplex', 'site')
                           for t in top),
    }


def main():
    samples = {
        '异常点': ['Blood Hideaway', 'Guristas Hideaway', "Sansha's Rally Point", 'Drone Assembly Plant'],
        '虫洞': ['Wolf-Rayet', 'C5 Wormhole sites', 'Sleeper Data Sanctuary'],
        '入侵': ['Nation Consolidation', 'True Power', 'Incursion sites'],
        'COSMOS': ['Amarr COSMOS', 'Otomainen VIII – Asteroid Belt 1'],
        '探索': ['Relic Site', 'Data Site', 'Ghost Sites'],
        '采矿': ['Ice Mining', 'Moon Mining'],
        '配置': ['Mission fittings', 'Epic Arc Fittings'],
        'Faction Warfare': ['Faction Warfare'],
    }
    out = {}
    for group, titles in samples.items():
        out[group] = []
        for t in titles:
            p = profile(t)
            out[group].append(p)
            if p.get('error'):
                print('%-16s %-34s ERROR %s' % (group, t[:34], p['error'][:40]))
            else:
                print('%-16s %-34s %6d 字符 %2d 节 %2d 表  模板=%s'
                      % (group, t[:34], p['chars'], p['sections'], p['tables'],
                         ','.join(p['templates'][:6])))
    json.dump(out, io.open('wiki_samples.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('\n→ wiki_samples.json')


if __name__ == '__main__':
    sys.exit(main())
