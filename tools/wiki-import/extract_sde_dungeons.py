"""从 EVE 官方 SDE 的 `dungeons.jsonl` 里抽出**旗舰远征**（capital escalation）的中英文名与官方描述。

为什么要这一步
--------------
wiki 的 `Combat site` 页里**没有**把旗舰级远征单独列出来（只在个别异常的"可升级为"里提过一两个），
所以 `fetch_wiki.py` 抓不到它们。但这批站点在游戏里是真实存在的，名字与描述都在 SDE 里，
而且 `dungeons.jsonl` 是**多语言**的（`name.en` / `name.zh` 同时存在），不需要机翻。

判定规则（`archetypeID = 31` 即 "Escalation / 远征"）
----------------------------------------------------
- `<Faction> Capital Staging` —— 旗舰集结地
- `Drone Capital Hive` —— 无人机旗舰机巢
- `<Faction> Shielded Starbase` —— 护盾加强母星
- `Drone Infested Starbase` —— 无人机侵袭的母星

排除 `Escalation Test 3/4`（CCP 的内部测试条目，游戏里不会出现）。

产物
----
- `capital_sites.json`：12 条旗舰远征，字段 en / zh / descZh / descEn / factionId / faction / sdeKey / kind
- `sde_names.json`：**全部** dungeon 的「英文名 → [{zh, factionId}]」映射（给 `build_sites.py` 用官方中文名纠名）

用法
----
    python extract_sde_dungeons.py "D:\\QEDSD\\SDE\\eve-online-static-data-3632936-jsonl"
"""

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'capital_sites.json')
OUT_NAMES = os.path.join(HERE, 'sde_names.json')

# SDE 的 factionID → 本项目沿用的势力索引（0 无人机 / 1 天蛇 / 2 血袭者 / 3 萨沙 / 4 天使 / 5 古斯塔斯）
FACTION_ID = {
    500021: 0,   # Rogue Drones
    500020: 1,   # Serpentis
    500012: 2,   # Blood Raiders
    500019: 3,   # Sansha's Nation
    500011: 4,   # Angel Cartel
    500010: 5,   # Guristas
}

PATTERNS = [
    ('staging', 'Capital Staging'),
    ('hive', 'Capital Hive'),
    ('starbase', 'Shielded Starbase'),
    ('infested', 'Infested Starbase'),
]


def strip_html(text):
    t = re.sub(r'<br\s*/?>', '\n', text or '', flags=re.I)
    t = re.sub(r'</p>', '\n', t, flags=re.I)
    t = re.sub(r'<[^>]+>', '', t)
    t = t.replace('&nbsp;', ' ').replace('&amp;', '&')
    lines = [' '.join(ln.split()) for ln in t.split('\n')]
    return '\n'.join(ln for ln in lines if ln).strip()


def main():
    if len(sys.argv) < 2:
        print('用法：python extract_sde_dungeons.py <SDE 目录>')
        return 1

    path = os.path.join(sys.argv[1], 'dungeons.jsonl')
    if not os.path.exists(path):
        print('找不到 dungeons.jsonl：%s' % path)
        return 1

    picked = []
    names = {}
    with io.open(path, encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            d = json.loads(line)
            name = d.get('name') or {}
            en = (name.get('en') or '').strip()
            zh = (name.get('zh') or '').strip()

            # 全量名字表（给纠名用）：同一个英文名可能对应多条 dungeon，都留着以便按势力挑
            if en and zh:
                names.setdefault(en, []).append({'zh': zh, 'factionId': d.get('factionID')})

            if d.get('archetypeID') != 31:
                continue
            if not en or en.startswith('Escalation Test'):
                continue
            low = en.lower()
            kind = None
            for tag, needle in PATTERNS:
                if needle.lower() in low:
                    kind = tag
                    break
            if kind is None:
                continue
            desc = d.get('description') or {}
            picked.append({
                'sdeKey': d['_key'],
                'en': en,
                'zh': zh or en,
                'descZh': strip_html(desc.get('zh', '')),
                'descEn': strip_html(desc.get('en', '')),
                'factionId': d.get('factionID'),
                'faction': FACTION_ID.get(d.get('factionID'), -1),
                'kind': kind,
            })

    # 同一英文名可能有多条 dungeon（不同等级/安等的同型站点），按英文名去重，保留 sdeKey 最小的
    picked.sort(key=lambda x: x['sdeKey'])
    seen = {}
    for p in picked:
        seen.setdefault(p['en'].lower(), p)
    result = sorted(seen.values(), key=lambda x: (x['faction'], x['kind']))

    json.dump(result, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('旗舰远征 %d 条 -> %s' % (len(result), OUT))
    for r in result:
        print('  %-32s %-22s faction=%-2s %s' % (r['en'], r['zh'], r['faction'], r['kind']))

    json.dump(names, io.open(OUT_NAMES, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    multi = sum(1 for v in names.values() if len(v) > 1)
    print('名字表 %d 条（其中 %d 个英文名对应多条 dungeon）-> %s' % (len(names), multi, OUT_NAMES))
    return 0


if __name__ == '__main__':
    sys.exit(main())
