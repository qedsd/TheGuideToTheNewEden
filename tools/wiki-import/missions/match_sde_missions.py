# -*- coding: utf-8 -*-
"""用游戏 SDE 的官方译名校对任务名。

SDE 的 `missions.jsonl`（2892 条）里每个任务都有 `name.en` / `name.zh` 等八语种**官方译名**，
这是最权威的对照——比 LLM 直译、比从旧库猜都可靠。

难点是两边的命名格式不同：
  wiki  `After The Seven - "Replacement"`        SDE `After The Seven (1 of 5) "Replacement"`
  wiki  `A Little Extra... (3 of 4)`             SDE `Undercover Clone - A Little Extra... (3 of 4)`
所以按四档逐级放宽匹配（见 `tiers()`），只在**唯一候选**时才认。

产出：
  sde_missions.json  —— {英文名小写: {en, zh, zhAll, briefing, keys}}
  sde_missing.json   —— 没能匹配上 SDE 的 wiki 标题
  parsed.json 里每页补 sdeZh / sdeBriefing（--apply 时写回）

用法：
  python match_sde_missions.py            # 只统计命中率
  python match_sde_missions.py --apply    # 写回 parsed.json
"""

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SDE_DIR = r"D:\QEDSD\SDE\eve-online-static-data-3632936-jsonl"
OUT = os.path.join(HERE, 'sde_missions.json')
PARSED = os.path.join(HERE, 'parsed.json')

PLACEHOLDER = re.compile(r'\{[^}]*\}|<[^>]+>')
PARENS = re.compile(r'\([^)]*\)')
NON_WORD = re.compile(r'[^0-9a-z\u4e00-\u9fff]+')
# 中文名尾巴上的分段号：`(1/5)` / `（2/5）`
PART_NO = re.compile(r'[\(（]\s*\d+\s*/\s*\d+\s*[\)）]\s*$')


def norm(s):
    s = (s or '').lower()
    for a, b in (('“', '"'), ('”', '"'), ('„', '"'), ('’', "'"), ('‘', "'"),
                 ('–', '-'), ('—', '-'), ('−', '-')):
        s = s.replace(a, b)
    return re.sub(r'\s+', ' ', s).strip()


def core(s):
    """去掉所有括号内容，再去掉非字母数字（保留 CJK），得到"核心名"。"""
    return NON_WORD.sub(' ', PARENS.sub(' ', norm(s))).strip()


def tiers(title):
    """由严到宽的候选键。"""
    n = norm(title)
    c = core(title)
    out = [('exact', n), ('core', c)]
    return out


def load_sde():
    path = os.path.join(SDE_DIR, 'missions.jsonl')
    rows = [json.loads(l) for l in io.open(path, encoding='utf-8') if l.strip()]

    by_en = {}
    for r in rows:
        name = r.get('name') or {}
        en = (name.get('en') or '').strip()
        zh = (name.get('zh') or '').strip()
        if not en:
            continue
        e = by_en.setdefault(en.lower(), {'en': en, 'zh': set(), 'briefing': ''})
        if zh:
            e['zh'].add(zh)
        if not e['briefing']:
            for m in r.get('messages') or []:
                if 'briefing' in (m.get('_key') or '').lower() and m.get('zh'):
                    text = re.sub(r'\s+', ' ', PLACEHOLDER.sub('', m['zh'])).strip()
                    if len(text) > 40:
                        e['briefing'] = text
                    break

    out = {}
    for k, v in by_en.items():
        out[k] = {
            'en': v['en'],
            # SDE 里同一英文名可能挂了两套中文（"警方介入" / "警方侵略"）：
            # 只取第一套，绝不把两套用 " / " 拼起来——拼起来会原样显示在任务名里（踩过）
            'zh': sorted(v['zh'])[0] if v['zh'] else '',
            'zhAll': sorted(v['zh']),
            'briefing': v['briefing'],
        }
    return out


def load_npc_names():
    """SDE 的 npcCharacters 里有 NPC 官方中文名——史诗任务线不少页面是用代理人/角色名命名的
    （Aakeo Oshaima → 艾可·奥赛玛），靠任务名匹配不上，靠这个兜底。"""
    path = os.path.join(SDE_DIR, 'npcCharacters.jsonl')
    out = {}
    for line in io.open(path, encoding='utf-8'):
        if not line.strip():
            continue
        r = json.loads(line)
        n = r.get('name') or {}
        en = (n.get('en') or '').strip()
        zh = (n.get('zh') or '').strip()
        if en and zh:
            out.setdefault(en.lower(), zh)
    return out


def build_index(sde):
    """两套索引：精确名、核心名（核心名可能一对多，只保留唯一候选）。"""
    exact, cores = {}, {}
    for k, v in sde.items():
        exact[k] = v
        c = core(v['en'])
        if c:
            cores.setdefault(c, []).append(v)
    return exact, cores


def part_of(name):
    m = re.search(r'\((\d+)\s+of\s+(\d+)\)', name or '', re.I)
    return (int(m.group(1)), int(m.group(2))) if m else None


def match(title, exact, cores, npc=None):
    want = part_of(title)
    for kind, key in tiers(title):
        if not key:
            continue
        if kind == 'exact' and key in exact:
            return exact[key], 'exact'
        if kind == 'core':
            cands = cores.get(key)
            if not cands:
                continue
            # 分段号必须一致：wiki 的 `Secret Project X (2 of 3)` 不能对到 SDE 的 `(1 of 3)`
            if want:
                cands = [c for c in cands if part_of(c['en']) in (None, want)]
            if not cands:
                continue
            if len({c['zh'] for c in cands}) == 1:
                return cands[0], 'core'
            if len(cands) == 1:
                return cands[0], 'core'
            # 一对多但只是"同一任务的第 n/m 段"：取公共基名（Human Cattle → 人·畜），
            # 分段号由 wiki 侧的多变体后缀去区分
            bases = {PART_NO.sub('', c['zh']).strip() for c in cands}
            if len(bases) == 1:
                base = bases.pop()
                if base:
                    hit = dict(cands[0])
                    hit['zh'] = base
                    return hit, 'core-base'
    # 最后一档：wiki 核心名是某个 SDE 核心名的"后缀"（Undercover Clone - A Little Extra...）
    # 人物/代理人名兜底（npcCharacters 的官方中文名）
    if npc:
        for cand in (title, PARENS.sub(' ', title)):
            hit = npc.get(norm(cand))
            if hit:
                return {'en': cand.strip(), 'zh': hit, 'briefing': ''}, 'npc'
    key = core(title)
    if len(key) >= 8:
        suffix_hits = [v for c, v in cores.items()
                       if c.endswith(' ' + key) and len({x['zh'] for x in v}) == 1]
        if len(suffix_hits) == 1:
            return suffix_hits[0][0], 'suffix'
    return None, ''


def main():
    sde = load_sde()
    exact, cores = build_index(sde)
    npc = load_npc_names()
    parsed = json.load(io.open(PARSED, encoding='utf-8'))

    stats = {}
    missing = []
    for page in parsed:
        hit, kind = match(page['title'], exact, cores, npc)
        if hit and hit['zh']:
            page['sdeZh'] = hit['zh']
            page['sdeEn'] = hit['en']
            if hit['briefing']:
                page['sdeBriefing'] = hit['briefing']
            stats[kind] = stats.get(kind, 0) + 1
        else:
            page.pop('sdeZh', None)
            missing.append(page['title'])

    hit_total = sum(stats.values())
    print('SDE 任务 %d 条，wiki 页 %d 条：命中 %d（%.1f%%），未命中 %d'
          % (len(sde), len(parsed), hit_total, 100.0 * hit_total / max(1, len(parsed)), len(missing)))
    for k, v in sorted(stats.items(), key=lambda kv: -kv[1]):
        print('   %-8s %5d' % (k, v))
    print('未命中样例：', ' | '.join(missing[:8]))

    json.dump(sde, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    json.dump(missing, io.open(os.path.join(HERE, 'sde_missing.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    if '--apply' in sys.argv:
        json.dump(parsed, io.open(PARSED, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print('已写回 parsed.json')
    return 0


if __name__ == '__main__':
    sys.exit(main())
