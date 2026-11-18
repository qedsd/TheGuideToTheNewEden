# -*- coding: utf-8 -*-
"""逐条核对"没能自动匹配上 SDE 官方译名"的 190 条，给出处置结论。

190 条逐条看下来，其实只有三类：

* **exclude（剔除）**：不是任务页，不该出现在任务列表里
  - 用户沙盒 `User:…`、`Storyline mission reports`、`Epic Arc Navigation List`、`Pirate epic arc advice`
  - 任务线章节页 `X/Chapter 1 - …`、`Amarr COSMOS` 这类枢纽
  - 系列总览页（`After The Seven`、`Air Show!`、`Pot and Kettle`……本身没有独立的游戏内任务名）
  - 同一任务的重复页（`Driving_a_Wedge_-_…` 下划线标题）
* **map（对到官方名）**：确实是任务，且 SDE 里有对应条目（含"同一系列按分段号对上"）
* **keep（保留）**：SDE 里确实没有的任务（COSMOS 子任务、活动任务、wiki 独有的分支），
  保持社区名/直译名，但把英文补齐成中文

输出 `sde_review.json`：{title, action, reason, sdeEn, sdeZh}
"""

import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
MISS = os.path.join(HERE, 'sde_missing.json')
SDE = os.path.join(HERE, 'sde_missions.json')
OUT = os.path.join(HERE, 'sde_review.json')

SERIES_HUB = {
    'after the seven', 'air show!', 'angel sound', 'fractured legacy', 'penumbra',
    'silence rahsa', 'smash and grab', 'syndication', 'the blood-stained stars',
    'the missing reporters', 'pot and kettle', 'whispers in the dark', 'the kidnapping',
    'flame of peace', "what's in a name", 'driving a wedge',
    'driving a wedge (alternate variant)', 'guristas strike', 'the major',
    'hunting a heretic', 'regional stability', 'the gallente archaeologist',
    'the numon claim', 'kidnappers strike', 'kidnappers strike part 1-4',
    'angel strike (blood raiders)', "angel strike (guristas pirates)",
    "angel strike (sansha's nation)",
}

JUNK = {
    'user:hirmuolio pine/sandbox', 'user:hirmuolio pine/sandbox3',
    'storyline mission reports', 'epic arc navigation list', 'pirate epic arc advice',
    "user:luxxianna seraph/sandbox/against the empire",
    'user:johnplayernotspecial/epic air fit', 'user:johnplayernotspecial/epic amarr fit',
    'user:johnplayernotspecial/epic caldari fit', 'user:johnplayernotspecial/epic gallente fit',
    'user:johnplayernotspecial/epic minmatar fit', 'user:johnplayernotspecial/epic ore fit',
    'user:johnplayernotspecial/epic soe fit', 'user:koris the second',
}

CAREER_HUB = {
    'enforcer (career agent)', 'exordium', 'explorer',
    'industrialist (entrepreneur)', 'industrialist (producer)', 'soldier of fortune',
}

COSMOS_HUB = re.compile(r'^(amarr|caldari|gallente|minmatar|angel|blood raider|guristas|serpentis) cosmos$', re.I)
UNDERSCORE_DUP = re.compile(r'_')

# 系列 → SDE 里的实际系列名（wiki 与客户端的叫法不同，靠分段副标题一一对上）
SERIES_ALIAS = {
    'angel strike': 'kidnappers strike',      # 10 段副标题完全相同，同一系列不同叫法
    'what\'s in a name': "what's in a name",
    'the ani build-up': 'the ani buildup',
    'supplies for the colonies': 'supplies for the colony',
    'ghost wars': 'ghost war',
    'striking down two flies with one swing': 'striking down two flies in one swing',
    'child of the nation': 'child of the nation',
}

# 手工指定（分段号对不上的、wiki 副标题与 SDE 不同的）
MANUAL = {
    'amarr excavators': 'Amarrian Excavators',
    'father and son': 'Fathers and Sons',
    'falling the nation comes at a cost': 'Failing The Nation Comes At A Cost',
    'lamb amongst lions': 'Lamb Among Lions',
    'clear the traderoute': 'Clear the Trade Route',
    'business associates - prologue': 'Business Associates - Prelude',
    'small misunderstanding': 'Small Misunderstanding (1 of 3)',
    'another slave rescue': 'Another Slave Rescue (1 of 3)',
    'mordus folly': "Mordu's Folly (1 of 2)",
    'building blocks 2': 'Building Blocks (2 of 2)',
    'travel to amarr': 'Travel to the Amarr',
    'travel to minmatar': 'Travel to the Minmatar',
    'the left hook': 'A Left Hook',
    'guristas strike - final battle': 'Guristas Strike - The Final Battle (10 of 10)',
    'requiem for a peace - the redemption (3 of 3)': 'Requiem for a Peace - Sentiment (3 of 3)',
    'corpse dealer - bring back the corpse part (2 of 3)': 'Corpse Dealer - Bring Back the Goods (2 of 3)',
    'corpse dealer - kutill’s hacker program part (3 of 3)': "Corpse Dealer - Kutill's Hacker Program (3 of 3)",
    'corpse dealer - the bodypart trade part (1 of 3)': 'Corpse Dealer - The Bodypart Trade (1 of 3)',
    'driving a wedge - ammbasidorial ambush': 'Driving a Wedge - Ambassadorial Ambush (1 of 2)',
    'driving a wedge - consulate conflagaration': 'Driving a Wedge - Consulate Conflagration (2 of 2)',
    'breaking tradition - the shaman’s abode part 1 (1 of 3)': "Breaking Tradition - The Shaman's Abode (1 of 3)",
    'the plantation workers - extra hand (3 of 4)': 'The Plantation Workers - Extra Hands (3 of 4)',
    'hunting a heretic': None,
}

# 保留但 SDE 无对应（COSMOS / 活动 / wiki 分支），reason 写在导出里
KEEP_REASON = {
    'booster manufacturing': 'SDE 里这 8 条按产出星系命名（E-8CSQ / 09-4XW…），与 wiki 的药剂名对不上；保留 wiki 名并翻译药剂名',
    'epidemic - food relief (2 of 2)': 'SDE 第 2 段叫 Out of Hand，与 wiki 的 Food Relief 不一致，先保留',
    'hidden truths': 'SDE 只有第 1 段，wiki 的 2/3 段无对应',
    'clone delivery': 'SDE 无此系列',
    'curiosity': 'SDE 无（候选 Curiosity Shop 是另一个任务）',
    'smash the shipyard': 'SDE 无',
    'save the trees': 'SDE 无',
    'for the troops': 'SDE 无',
    'agent failed': 'SDE 无',
    'endless battle': 'COSMOS 子任务，SDE 无',
    'fair play': 'COSMOS 子任务，SDE 无',
    'labor of love': 'COSMOS 子任务，SDE 无',
    'troop build up': 'COSMOS 子任务，SDE 无',
    'undue attention': 'COSMOS 子任务，SDE 无',
    'vitoc vector': 'COSMOS 子任务，SDE 无',
    'a new venture': 'COSMOS 子任务，SDE 无',
    'greater good': 'COSMOS 子任务，SDE 无',
    'new slaves': 'COSMOS 子任务，SDE 无',
    'humble beginnings': 'COSMOS 子任务，SDE 无',
    'khanid nobleman': 'COSMOS 子任务，SDE 无',
    'intaki and mordus legion': 'COSMOS 子任务，SDE 无',
    'new frontiers 1-7': 'wiki 汇总页，SDE 无',
    'elohim x-instinct production facility': 'COSMOS 地点页，SDE 无',
}


# SDE 里确实没有的任务：中文名自己定（增效剂名用 SDE types.jsonl 的官方译名）
KEEP_ZH = {
    'agent failed': '代理人失手',
    'booster manufacturing (blue pill)': '制造增效剂（蓝色药丸）',
    'booster manufacturing (crash)': '制造增效剂（克拉什）',
    'booster manufacturing (drop)': '制造增效剂（坠落）',
    'booster manufacturing (exile)': '制造增效剂（游离）',
    'booster manufacturing (frentix)': '制造增效剂（疯癫）',
    'booster manufacturing (mindflood)': '制造增效剂（思维冲击）',
    'booster manufacturing (sooth sayer)': '制造增效剂（梦呓）',
    'booster manufacturing (x-instict)': '制造增效剂（X—本能）',
    'clone delivery (1 of 4)': '克隆体递送（1/4）',
    'curiosity': '好奇心',
    'elohim x-instinct production facility': '以罗欣 X—本能生产设施',
    'endless battle': '无尽的战斗',
    'epidemic - food relief (2 of 2)': '瘟疫 - 食物救济（2/2）',
    'fair play': '公平竞争',
    'hidden truths - unfolding a mystery (2 of 3)': '隐藏的真相 - 揭开谜团（2/3）',
    'hidden truths - extremely unfortunate incident (3 of 3)': '隐藏的真相 - 极其不幸的事件（3/3）',
    'humble beginnings': '卑微的起点',
    'intaki and mordus legion': '因塔基与莫德团',
    'khanid nobleman': '卡尼德贵族',
    'labor of love': '乐意为之',
    'new frontiers 1-7': '新边疆 1-7',
    'smash the shipyard': '捣毁船坞',
    'troop build up': '集结部队',
    'undue attention': '过分的关注',
    'vitoc vector': '崴砣柯带菌者',
    'a new venture': '新的冒险',
    'for the troops': '犒劳部队',
    'greater good': '更大的利益',
    'new slaves': '新的奴隶',
    'save the trees': '保护树木',
    "kidnappers strike part 5-10 (blood raiders)": '绑架者的进攻 第 5-10 段（血袭者）',
    "kidnappers strike part 5-10 (guristas pirates)": '绑架者的进攻 第 5-10 段（古斯塔斯）',
    "kidnappers strike part 5-10 (sansha's nation)": '绑架者的进攻 第 5-10 段（萨沙）',
    # SDE 这条存了两个并列译名（天使的进攻 / 绑架者的进攻），按同系列其它 9 段统一
    'angel strike - the flu outbreak (6 of 10)': '绑架者的进攻—流感爆发（6/10）',
}

def part_number(title):
    m = re.search(r'\((\d+)\s+of\s+(\d+)\)', title, re.I)
    return (int(m.group(1)), int(m.group(2))) if m else (None, None)


def series_of(title):
    """取 `A - B (1 of 4)` 里的 A 段。"""
    return re.split(r'\s+-\s+|\s+\(', title, maxsplit=1)[0].strip()


def review(title, sde):
    low = title.strip().lower()

    if low in JUNK or low.startswith('user:'):
        return 'exclude', '用户沙盒/非任务页'
    if '/chapter ' in low:
        return 'exclude', '任务线章节页（枢纽）'
    if COSMOS_HUB.match(title):
        return 'exclude', 'COSMOS 枢纽页'
    # 职业代理人的页面（Exordium / Explorer / Enforcer…）**不能剔除**：
    # 它们一个页面里塞了 7–8 个 {{Missiondetails}}，正是那 45 条职业任务本体（早先剔过一次，少了 45 条）
    if low in SERIES_HUB:
        return 'exclude', '任务系列总览页（无独立游戏内任务名）'
    # `What's In a Name (1 of 5)` 与 `What's in a Name - "Change of Address"` 是同一任务的
    # 两套 wiki 页面（后者带副标题、信息更全），保留后者，前者按重复页剔除
    if re.match(r"^what.s in a name\s*\(\d+\s*of\s*\d+\)$", low):
        return 'exclude', '与带副标题的同一任务页重复（保留 `- "副标题"` 那套）'
    if UNDERSCORE_DUP.search(title):
        return 'exclude', '同一任务的下划线重复页（wiki 标题带 _ 的那份）'

    # 明确"SDE 里没有"的先判，免得被下面的系列前缀规则误吞
    for k, reason in KEEP_REASON.items():
        if low.startswith(k):
            return 'keep', reason

    # 手工指定
    for k, v in MANUAL.items():
        if low == k:
            if v is None:
                return 'keep', '已手工判定：保留原译名'
            return 'map', v

    # 系列按分段号对齐
    series = series_of(title)
    alias = SERIES_ALIAS.get(series.lower())
    if alias is None:
        # 没改名的系列（Flame of Peace / The Major / Regional Stability…）直接用系列名当前缀
        probe = series.lower() + ' -'
        if any(k.startswith(probe) for k in sde):
            alias = series
    if alias:
        n, total = part_number(title)
        if n and total:
            prefix = alias.lower()
            for k, v in sde.items():
                if not k.startswith(prefix):
                    continue
                m = re.search(r'\((\d+)\s+of\s+(\d+)\)', v['en'])
                if m and int(m.group(1)) == n and int(m.group(2)) == total:
                    return 'map', v['en']
            return 'keep', 'SDE 里这个系列没有第 %d 段' % n

        # 没有分段号但 SDE 只有唯一一条同系列条目 → 直接对上（一箭双雕的 3 个势力版本）
        same = [v for k, v in sde.items() if k.startswith(alias.lower())]
        if len(same) == 1:
            return 'map', same[0]['en']
        return 'keep', '系列总览（无分段号），保留'

    return 'keep', 'SDE 无对应条目，保留现有译名'


def main():
    sde = json.load(io.open(SDE, encoding='utf-8'))
    miss = json.load(io.open(MISS, encoding='utf-8'))

    out = []
    stats = {}
    for title in miss:
        action, detail = review(title, sde)
        entry = {'title': title, 'action': action}
        if action == 'map':
            hit = sde.get(detail.lower())
            entry['sdeEn'] = detail
            # SDE 里个别条目有两个并列译名（"天使的进攻—流感爆发 / 绑架者的进攻—流感爆发"），取第一个
            entry['sdeZh'] = (hit['zh'].split(' / ')[0] if hit else '')
            entry['reason'] = '对齐到 SDE：' + detail
        else:
            entry['reason'] = detail
        # 人工指定的中文名优先于 SDE（SDE 里个别条目有并列译名或明显笔误）
        low = title.strip().lower()
        zh = KEEP_ZH.get(low)
        if zh is None:
            for k, v in KEEP_ZH.items():
                if low.startswith(k):
                    zh = v
                    break
        if zh:
            entry['sdeZh'] = zh
        out.append(entry)
        stats[action] = stats.get(action, 0) + 1

    json.dump(out, io.open(OUT, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('逐条核对结果：', stats)

    if '--apply' in sys.argv:
        apply_to_parsed(out)

    for title, action in [('map', '对齐'), ('keep', '保留'), ('exclude', '剔除')]:
        print('\n=== %s（%d）===' % (action, stats.get(title, 0)))
        for e in out:
            if e['action'] != title:
                continue
            tail = e.get('sdeZh') or e['reason']
            print('   %-58s %s' % (e['title'][:58], tail[:60]))
    return 0


def apply_to_parsed(out):
    """把结论写回 parsed.json：map → sdeZh，exclude → excluded=True（build 时跳过）。"""
    parsed = json.load(io.open(os.path.join(HERE, 'parsed.json'), encoding='utf-8'))
    by_title = {e['title']: e for e in out}
    renamed = dropped = 0
    for page in parsed:
        e = by_title.get(page['title'])
        if not e:
            continue
        if e['action'] == 'exclude':
            page['excluded'] = True
            dropped += 1
        else:
            # 之前几轮可能标过 excluded，这里必须显式清掉，否则改判会失效
            page['excluded'] = False
            if e.get('sdeZh'):
                page['sdeZh'] = e['sdeZh']
                renamed += 1
    json.dump(parsed, io.open(os.path.join(HERE, 'parsed.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)
    print('\n写回 parsed.json：改名 %d，剔除 %d' % (renamed, dropped))


if __name__ == '__main__':
    raise SystemExit(main())
