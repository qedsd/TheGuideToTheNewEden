# -*- coding: utf-8 -*-
"""把 parsed.json 渲染成中文 Markdown 并写进 static.db 的 Mission 表。

渲染口径（与战斗站点页保持一致，用户切页面不用换脑子）：
  **分类** 安全任务 ｜ **等级** 4 ｜ **类型** 战斗 ｜ **势力** 天使
  ### 任务目标        ← infobox 的 Objective
  ### 属性            ← EWAR / 船型限制 / 建议船型 / 奖励 / 声望损失 / 备注（有才列）
  ### 简报            ← {{MissionBriefing}}（代理人原话）
  ## 正文小节…        ← wiki 的 == 小节 ==
  表格               ← {{NPCTableHead/Row}} 转成 Markdown 表
  > 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 …

译文来自本目录的 translations.json：{sha1(英文片段) → 中文}。缺译文时**回退英文**并在正文顶部标注，
这样"数据先跑通、译文后补"不会卡住整条流水线。

用法：
  python build_missions.py                  # 只生成 missions.json 便于检查
  python build_missions.py --write-db       # 建表并写入 static.db（同时删掉老的 MissionContent）
"""

import hashlib
import io
import json
import os
import re
import sqlite3
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PARSED = os.path.join(HERE, 'parsed.json')
TRANS = os.path.join(HERE, 'translations.json')
# 旧版工具里的社区/游戏内中文任务名（对齐到英文标题），玩家就是按这些名字找任务
ALIGN_FILE = os.path.join(HERE, 'align_merged.json')   # merge_align.py 产出（多轮对齐的合集）
OUT_JSON = os.path.join(HERE, 'missions.json')

STATIC_DB = (r"D:\QEDSD\Code\TheGuideToTheNewEden\TheGuideToTheNewEden"
             r"\TheGuideToTheNewEden.WPF\Resources\Database\static.db")
SYNC_DATE = '2026-11-17'

CATEGORY_CN = {
    0: '安全任务', 1: '运输任务', 2: '采矿任务', 3: '故事线任务', 4: '史诗任务',
    5: '异常任务', 6: '空中代理人', 7: '研发任务', 8: '职业代理人', 9: 'Circle 任务',
}

# 0–5 是海盗势力（与战斗站点同一套编号），6–9 是四大帝国：
# 任务里"针对四大种族"的那一类（Enemies Abound 之类）Faction1 写的就是 Gallente Federation / Minmatar Republic。
FACTION_CN = {0: '无人机', 1: '天蛇', 2: '血袭者', 3: '萨沙', 4: '天使', 5: '古斯塔斯',
              6: '艾玛帝国', 7: '加达里合众国', 8: '盖伦特联邦', 9: '米玛塔尔共和国'}

# wiki 的 Faction/Faction1 取值 → 本项目势力索引
FACTION_MAP = {
    'rogue drones': 0, 'rogue drone': 0,
    'serpentis': 1,
    'blood raider': 2, 'blood raiders': 2,
    'sanshas nation': 3, 'sansha': 3,
    'angel cartel': 4, 'angels': 4,
    'guristas': 5,
    'amarr empire': 6, 'amarr': 6,
    'caldari state': 7, 'caldari': 7,
    'gallente federation': 8, 'gallente': 8,
    'minmatar republic': 9, 'minmatar': 9,
}

# 任务类型（Type 原值）→ 中文
TYPE_CN = {
    'encounter': '战斗',
    'courier': '运输',
    'mining': '采矿',
    'trade': '交易',
    'talk to agent': '对话',
    'agent interaction': '分支选择',
    'storyline(encounter)': '故事线（战斗）',
    'storyline(courier)': '故事线（运输）',
    'storyline(mining)': '故事线（采矿）',
    'storyline(trade)': '故事线（交易）',
    'storyline': '故事线',
}

# infobox 里这些值极短且重复率高，走本地词典，不占翻译预算
VALUE_CN = {
    'frigate': '护卫舰', 'destroyer': '驱逐舰', 'cruiser': '巡洋舰',
    'battlecruiser': '战列巡洋舰', 'battleship': '战列舰', 'industrial': '工业舰',
    'hauler': '运输舰', 'freighter': '货舰', 'mining barge': '采掘者',
    'exhumer': '逸灵者', 'none': '无', 'any': '不限',
    'web': '网子', 'scrambler': '扰频器', 'scram': '扰频器', 'point': '反跳',
    'neut': '中和器', 'energy neutralizer': '能量中和器',
    'sensor dampener': '感应抑阻', 'damp': '感应抑阻',
    'tracking disruptor': '追踪干扰', 'td': '追踪干扰',
    'ecm': 'ECM 干扰', 'jam': 'ECM 干扰',
}

SECTION_CN = {
    'walkthrough': '攻略',
    'walkthrough & notes': '攻略与备注',
    'strategy': '打法',
    'notes': '备注',
    'tips': '小贴士',
    'loot': '掉落',
    'rewards': '奖励',
    'objective': '任务目标',
    'objectives': '任务目标',
    'briefing': '简报',
    'mission briefing': '简报',
    'enemy npcs': '敌方 NPC',
    'npcs': 'NPC',
    'structures': '建筑',
    'mining': '采矿',
    'transport': '运输',
    'hints': '提示',
    'video': '视频',
    'see also': '参见',
    'references': '参考',
    'external links': '外部链接',
}


def key(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:16]


def tr(text):
    """片段翻译：命中缓存返回中文，否则原样返回英文。"""
    if not text:
        return ''
    text = clean_value(text) if ('{{' in text or '<' in text) else text
    t = text.strip()
    if not t:
        return ''
    hit = TRANS.get(key(t))
    return hit if hit else t


def cn_short(value):
    """短值翻译：先查本地词典（大小写无关），再查译文缓存。"""
    if not value:
        return ''
    v = value.strip()
    low = v.lower()
    if low in VALUE_CN:
        return VALUE_CN[low]
    if low in TYPE_CN:
        return TYPE_CN[low]
    return tr(v)


def tr_raw(value):
    """字段值翻译：先按**原文**查译文缓存（缓存键就是原文的 sha1[:16]），
    查不到再走 tr()（它会先清 wiki 语法再查一次），最后回退英文。"""
    raw = str(value or '').strip()
    if not raw:
        return ''
    hit = TRANS.get(key(raw))
    if hit:
        return hit
    return tr(raw)

# 属性字段里常见的"模板化英文"，用规则补齐（这些句子反复出现，走翻译批次不值）
PHRASE_RULES = [
    (r'This site contains special ship restrictions\.?', '本设施有特殊船型限制'),
    (r'This is an important mission,? which will have significant impact on your standings\.?',
     '这是重要任务，会显著影响你的声望'),
    (r'Low sec', '低安'),
    (r'High sec', '高安'),
    (r'Collateral of ([\d,]+) ISK required', r'需要 \1 ISK 押金'),
    (r'(\d+)\s*jumps?', r'\1 跳'),
    (r'bonus', '奖励'),
    (r'storyline BPC', '故事线蓝图拷贝'),
    (r'faction standings', '势力声望'),
    (r'\b(\d+) run\b', r'\1 次'),
    (r'\((\d+)\s*h(?:ours?)?\)', r'（\1 小时）'),
]

# 单个词就是"舰型/电子战"时直接映射（ShipSuggestion / ShipLimit / Ewar 列大量出现）
VALUE_ALIAS = {
    'none': '', '?': '', 'n/a': '', '-': '',
    'web': '网子', 'neut': '中和器', 'ecm': 'ECM', 'web paint': '网子 + 目标标记',
    'point': '反跳', 'scram': '扰频器', 'scrambler': '扰频器',
    'travel fit': '旅行配置', 't1 cruiser': 'T1 巡洋舰',
    'blockade runner': '封锁穿越舰',
}


def item_value(raw):
    """字段值：先走译文，仍是英文则按 SDE 物品名补中文。"""
    out = clean_value(tr_raw(raw))
    if out and not CJK_HAS(out):
        out = annotate_items(out)
    return out


def apply_phrase_rules(text):
    if not text:
        return text
    low = text.strip().lower()
    alias = VALUE_ALIAS.get(low)
    if alias is None:
        # 舰型词复用 NPC 表词典（CLASS_CN 在本文件后面才定义，所以运行时查）
        alias = CLASS_CN.get(low)
    if alias is None and low in ('covert ops transport', 'covert ops'):
        alias = {'covert ops transport': '隐形特勤运输舰', 'covert ops': '隐形特勤舰'}[low]
    if alias is not None:
        return alias
    out = text
    for pattern, repl in PHRASE_RULES:
        out = re.sub(pattern, repl, out, flags=re.I)
    return out

def clean_value(v):
    """infobox 里的字段值可能带 <sup>3</sup> / [[链接]] 之类，落库前清干净。"""
    import re as _re
    if not v:
        return ''
    s = v
    # 常见"带内容"的模板先拆成纯文本：{{m3|320}} / {{Sh|Frigate}} / {{Co|lime|文字}} / {{icon|web|24|说明}}
    s = _re.sub(r'\{\{\s*m3\s*\|([^}|]*)[^}]*\}\}', r'\1 m3', s, flags=_re.I)
    s = _re.sub(r'\{\{\s*(?:co|col|color)\s*\|[^}|]*\|([^}|]*)[^}]*\}\}', r'\1', s, flags=_re.I)
    s = _re.sub(r'\{\{\s*icon\s*\|[^}|]*\|[^}|]*\|([^}|]*)[^}]*\}\}', r'\1', s, flags=_re.I)
    s = _re.sub(r'\{\{\s*(?:sh|sk|damagetype|dt)\s*\|([^}|]*)[^}]*\}\}', r'\1', s, flags=_re.I)
    # 兜底：剩下的 {{…}} 整块删掉（wiki 语法不该出现在正文里）
    for _ in range(20):
        m = _re.search(r'\{\{[^{}]*\}\}', s)
        if not m:
            break
        s = s[:m.start()] + ' ' + s[m.end():]
    # 括号不平衡的模板（`{{ColorSecurityRating` 没闭合）会把后面整段吞掉，直接截断
    if '{{' in s:
        cut = s.find('{{')
        s = s[:cut]
    s = _re.sub(r'</?(sup|sub|span|small|big|br|em|strong|tt|code|nowiki)[^>]*>', '', s, flags=_re.I)
    s = _re.sub(r'\[\[([^\]\|]+)\|([^\]]+)\]\]', r'\2', s)
    s = _re.sub(r'\[\[([^\]]+)\]\]', r'\1', s)
    s = s.replace('&nbsp;', ' ')
    s = _re.sub(r'\s+', ' ', s).strip()
    return apply_phrase_rules(s)


def faction_of(info):
    raw = ' '.join(str(info.get(k, '')) for k in ('Faction', 'Faction1', 'Faction2')).lower()
    for name, idx in FACTION_MAP.items():
        if name in raw:
            return idx
    return -1


def strip_suffix(title):
    """`Alluring Emanations (Level 2)` → (`Alluring Emanations`, 2)"""
    m = re.match(r'^(.*?)\s*\(Level\s*(\d)\)$', title)
    if m:
        return m.group(1), int(m.group(2))
    return title, 0


# NPC 表"舰型"列：wiki 上就这么 50 来种写法（含错别字与复数），直接本地词典，
# 不走翻译批次——闭集、量大（近 8000 行），一次配好永久生效。
CLASS_CN = {
    'frigate': '护卫舰', 'frigates': '护卫舰',
    'elite frigate': '精英护卫舰', 'elite frigates': '精英护卫舰',
    'friendly frigate': '友方护卫舰', 'commander frigate': '指挥官护卫舰',
    'neutral frigate': '中立护卫舰',
    'destroyer': '驱逐舰', 'elite destroyer': '精英驱逐舰',
    'cruiser': '巡洋舰', 'cruisers': '巡洋舰', 'cruier': '巡洋舰',
    'elite cruiser': '精英巡洋舰', 'elite cruiser?': '精英巡洋舰',
    'friendly cruiser': '友方巡洋舰', 'neutral cruiser': '中立巡洋舰',
    'commander cruiser': '指挥官巡洋舰',
    'battlecruiser': '战列巡洋舰', 'battlecruisers': '战列巡洋舰',
    'battlecruiser?': '战列巡洋舰',
    'elite battlecruiser': '精英战列巡洋舰', 'commander battlecruiser': '指挥官战列巡洋舰',
    'battleship': '战列舰', 'battleships': '战列舰',
    'elite battleship': '精英战列舰', 'friendly battleship': '友方战列舰',
    'neutral battleship': '中立战列舰', 'commander battleship': '指挥官战列舰',
    'sentry': '哨塔', 'friendly sentry': '友方哨塔',
    'structure': '建筑', 'tower': '炮塔', 'container': '货柜',
    'asteroid': '小行星', 'astroids': '小行星',
    'drone': '无人机', 'fighter': '铁骑舰载机', 'fighters': '铁骑舰载机',
    'acceleration gate': '加速门', 'accl gate': '加速门', 'beacon': '信标',
    'industrial': '工业舰', 'hauler': '运输舰', 'freighter': '货舰',
    'barge': '采掘驳船', 'shuttle': '穿梭机', 'carrier': '航母',
    'supercarrier': '超级航母', 'hangar array': '机库阵列', 'station': '空间站',
    'hackable': '可破解设施', 'gas cloud': '气云',
    'none': '无', '?': '未知', '??': '未知',
}


def class_cn(value):
    """舰型列专用：先查闭集词典，查不到再走译文缓存。"""
    v = (value or '').strip()
    if not v:
        return ''
    v = re.sub(r'<!--.*?-->', '', v, flags=re.S).strip()
    # wiki 表格属性混进了参数里：style="text-align: right;" / rowspan= 2 —— 清掉
    v = re.sub(r'[A-Za-z_-]+\s*=\s*("[^"]*"|\'[^\']*\'|\S+)', ' ', v)
    v = re.sub(r'\s+', ' ', v).strip()
    if not v:
        return ''
    hit = CLASS_CN.get(v.lower())
    return hit if hit else cn_short(v)

SDE_DIR = r"D:\QEDSD\SDE\eve-online-static-data-3632936-jsonl"
_NPC_ZH = None


def npc_name_table():
    """SDE 里的 英文名 → 官方中文名（types 是舰船/建筑/炮塔，npcCharacters 是人物）。

    NPC 表里的舰名以前一律不译，用户反馈「Coreli Initiate / Blood Raider Sentry Gun」这种看不懂。
    SDE 这两张表合起来能覆盖一半以上的行，剩下的保留英文。
    """
    global _NPC_ZH
    if _NPC_ZH is not None:
        return _NPC_ZH
    table = {}
    for fname in ('types.jsonl', 'npcCharacters.jsonl'):
        path = os.path.join(SDE_DIR, fname)
        if not os.path.exists(path):
            continue
        for line in io.open(path, encoding='utf-8'):
            if not line.strip():
                continue
            row = json.loads(line)
            nm = row.get('name') or {}
            en = (nm.get('en') or '').strip()
            zh = (nm.get('zh') or '').strip()
            if en and zh:
                table.setdefault(en.lower(), zh)
                # 归一化索引：`Kruul's Pleasure Hub` vs `Kruuls Pleasure Hub`、多余空格/连字符。
                # 注意必须限定长度——中文名归一化后是空串，会把整张表塌成同一个键（踩过，
                # 结果「中和器」被当成某个英文名翻成"泰坦会战遗址"）
                norm_key = re.sub(r'[^0-9a-z]+', '', en.lower())
                if len(norm_key) >= 3:
                    table.setdefault('=' + norm_key, zh)
    _NPC_ZH = table
    return table


def npc_zh(name):
    """舰名 → 官方中文名；`A/B/C` 这种 wiki 简写会逐段解析，全中才返回。"""
    raw = re.sub(r'<!--.*?-->', '', name or '').strip()
    if not raw:
        return ''
    table = npc_name_table()

    def one(text):
        text = re.sub(r'\s+', ' ', text).strip()
        hit = table.get(text.lower())
        if hit:
            return hit
        norm_key = re.sub(r'[^0-9a-z]+', '', text.lower())
        if len(norm_key) >= 3:
            hit = table.get('=' + norm_key)
            if hit:
                return hit
        # 去掉尾部括号注（`Rogue Miner (Rogue Pirate Grunt)`）
        base = re.sub(r'\s*\([^)]*\)\s*$', '', text).strip()
        if base and base != text:
            return table.get(base.lower(), '')
        return ''

    parts = [p for p in re.split(r'\s*/\s*', raw) if p.strip()]
    hits = [one(p) for p in parts]
    if parts and all(hits):
        return '/'.join(hits)

    # wiki 的省略写法：`Guristas Kyoukan/Webifier` 其实是 "Guristas Kyoukan" + "Guristas Webifier"；
    # `Belphegor/Mammon Apis` 是 "Belphegor Apis" + "Mammon Apis"。两种共享前后缀都试一下。
    if len(parts) == 2:
        head, tail = parts
        head_words, tail_words = head.split(), tail.split()
        cands = []
        if len(head_words) >= 1 and len(tail_words) == 1:
            cands.append((' '.join(head_words[:-1] + tail_words), head))
        if len(tail_words) >= 1 and len(head_words) == 1:
            cands.append((tail_words[0] + ' ' + head, tail))
        for second_try, first_try in cands:
            a, b = one(first_try), one(second_try)
            if a and b:
                return a + '/' + b
    return ''


def npc_compound_zh(text):
    """`Coreli Initiate/Scout` 这类共享前缀的简写：逐段解析，缺前缀的补上前缀。"""
    parts = [p.strip() for p in re.split(r'\s*/\s*', text or '') if p.strip()]
    if not parts:
        return ''
    if len(parts) == 1:
        return npc_zh(parts[0])
    head = parts[0]
    head_words = head.split()
    resolved = [npc_zh(head)]
    for part in parts[1:]:
        zh = npc_zh(part)
        if not zh and head_words:
            # `Coreli Initiate/Scout` → `Coreli Scout`
            zh = npc_zh(' '.join(head_words[:-1] + part.split()))
        resolved.append(zh)
    if all(resolved):
        return '/'.join(resolved)
    return ''


def npc_cell(name):
    """表格"名称"列：有官方中文就 `中文（English）`，没有就只留英文。"""
    raw = clean_value(re.sub(r'<!--.*?-->', '', name or '').strip())
    # wiki 里没名字的行会写 `None`，直接留空比显示 None 好
    if raw.lower() in ('none', 'n/a', '-'):
        return ''
    zh = npc_zh(raw)
    if zh and zh != raw:
        return '%s（%s）' % (zh, raw)

    # `Seven Plebe [Coreli Initiate/Scout]`：方括号里才是舰型，翻译括号里的部分
    m = re.match(r'^(.*?)\s*\[([^\]]+)\]\s*$', raw)
    if m:
        zh_in = npc_compound_zh(m.group(2))
        if zh_in:
            return '%s（%s）' % (raw, zh_in)
    return raw


_NPC_ANNOT = None


def npc_annot_table():
    """备注列专用的词典：只收"NPC 表里出现过的舰名"。

    直接用全量 SDE 表会误伤——`drop`（药剂 Drop→坠落）、`Heavy Missiles`（技能书
    "重型导弹概论"）这类通用词都会被替换。所以这里只用任务表里真正出现过的舰名。
    """
    global _NPC_ANNOT
    if _NPC_ANNOT is not None:
        return _NPC_ANNOT
    table = {}
    pages = json.load(io.open(PARSED, encoding='utf-8'))
    for page in pages:
        for v in page.get('variants') or []:
            for sec in v.get('sections') or []:
                for tb in sec.get('npcs') or []:
                    for row in tb.get('rows') or []:
                        raw = re.sub(r'<!--.*?-->', '', row.get('name') or '').strip()
                        if not raw or raw.lower() in ('none', 'n/a', '-'):
                            continue
                        zh = npc_zh(raw)
                        if zh and zh != raw:
                            table.setdefault(raw.lower(), zh)
                        for part in re.split(r'\s*/\s*', raw):
                            pz = npc_zh(part)
                            if pz and pz != part:
                                table.setdefault(part.strip().lower(), pz)
    _NPC_ANNOT = table
    return table


def annotate_npc_names(text, max_tokens=6):
    """把自由文本里的舰名替换成 `中文（English）`（备注列用）。


    NPC 备注里经常点名（`Web by Mercenary Interceptor`、`Attacking Kruul triggers…`），
    逐 token 做最长匹配：从 6 个词往回试，命中就用中英对照替换。
    """
    text = (text or '').strip()
    if not text:
        return ''
    table = npc_annot_table()
    tokens = re.split(r'(\s+)', text)
    words = [t for t in tokens if t.strip()]
    seps = [t for t in tokens if not t.strip()]

    def lookup(chunk):
        chunk = chunk.strip().strip(',.;:!?()[]')
        if not chunk:
            return ''
        hit = table.get(chunk.lower())
        if hit:
            return hit
        norm_key = re.sub(r'[^0-9a-z]+', '', chunk.lower())
        if len(norm_key) >= 3:
            return table.get('=' + norm_key, '')
        return ''

    out = []
    i = 0
    while i < len(words):
        matched = False
        for n in range(min(max_tokens, len(words) - i), 0, -1):
            chunk = ' '.join(words[i:i + n])
            zh = lookup(chunk)
            if zh:
                out.append('%s（%s）' % (zh, chunk.strip().strip(',.;:!?()[]')))
                i += n
                matched = True
                break
        if not matched:
            out.append(words[i])
            i += 1
            # 保留原来的分隔符
            if i - 1 < len(seps):
                out.append(' ')
    return ''.join(out).replace('  ', ' ').strip()

def npc_table_md(table):
    rows = table.get('rows') or []
    if not rows:
        return ''
    out = []
    # NPCTableHead 的模板体同样以 `|` 开头，直接渲染会出现 `**|建筑**`；
    # 译文缓存里存的是**带 `|` 的原文**，所以先按原文查、再按去 `|` 的查一遍
    raw_title = table.get('title') or ''
    title = raw_title.lstrip('|').strip()
    if title:
        label = tr(raw_title)
        if label == raw_title:
            label = tr(title)
        out.append('**%s**' % label.lstrip('|').strip())
        out.append('')
    out.append('| 舰型 | 数量 | 名称 | 备注 |')
    out.append('|---|---|---|---|')
    for r in rows:
        notes = []
        if r.get('ewar'):
            notes.append('电子战：' + annotate_npc_names(cn_short(r['ewar'])))
        if r.get('trigger'):
            notes.append('触发：' + annotate_npc_names(tr(r['trigger'])))
        if r.get('note'):
            notes.append(annotate_npc_names(tr(r['note'])))
        out.append('| %s | %s | %s | %s |' % (
            class_cn(r.get('cls', '')), r.get('count', ''), npc_cell(r.get('name', '')),
            '；'.join(notes)))
    out.append('')
    return '\n'.join(out)


def section_title_cn(title):
    """小节标题翻译：常见标题走词典，其余查译文缓存。"""
    t = title.strip()
    low = t.lower()
    if low in SECTION_CN:
        return SECTION_CN[low]
    return tr(t)


def tr_block(text):
    """按**行**翻译一段正文：译文缓存是按行建的（导出的就是行），
    整段查永远查不到，之前因此整段落回英文。"""
    lines = []
    for line in (text or '').split('\n'):
        s = line.strip()
        if not s:
            lines.append('')
            continue
        # 分类/文件标记漏进正文（`[[Category:COSMOS missions}}`）——直接丢
        if re.match(r'^\[\[(?:Category|File|Image):', s, re.I) or s.startswith('{{'):
            continue
        if CJK_HAS(s) or s.startswith('|'):
            lines.append(line.rstrip())
            continue
        lines.append(tr(s))
    return '\n'.join(lines).strip()


def tr_paragraphs(text):
    """按空行切段，逐段查译文缓存（缓存键是单段原文）。"""
    if not text:
        return ''
    out = []
    for para in re.split(r'\n{2,}', text.strip()):
        p = para.strip()
        if not p:
            continue
        out.append(tr(p) if not CJK_HAS(p) else p)
    return '\n\n'.join(out)


def CJK_HAS(text):
    return bool(re.search(r'[\u4e00-\u9fff]', text or ''))


def annotate_items(text):
    """物品/舰船名补官方中文：`Dominix, Paladin` → `多米尼克斯级（Dominix）、帕拉丁级（Paladin）`。

    用 SDE types 的官方译名；逗号分隔的逐个处理，查不到就保留英文。
    """
    if not text:
        return text
    table = npc_name_table()

    def one(part):
        p = part.strip()
        if not p:
            return p
        # `1 x Gas Cloud Harvester I`：数量前缀要留着，只翻物品名
        m = re.match(r'^(\d+\s*[x×]\s*)(.+)$', p, re.I)
        qty, name = (m.group(1), m.group(2).strip()) if m else ('', p)
        zh = table.get(name.lower())
        if zh and zh != name:
            return '%s%s（%s）' % (qty, zh, name)
        return p

    parts = [p for p in re.split(r'[,，、/]\s*', text) if p.strip()]
    if len(parts) > 1:
        return '、'.join(one(p) for p in parts)
    return one(text)

# `pocket` 在 EVE 里指死亡空间中的一个独立区域，玩家口语叫"一层""二层"。
# 之前直译成了"口袋"（`Single Pocket` → 单口袋），读起来不知所云。
POCKET_RULES = [
    (r'单口袋', '单层区域'),
    (r'单一无门口袋', '单层无门区域'),
    (r'奖励口袋', '奖励区域'),
    (r'第\s*(\d+)\s*口袋', r'第 \1 层'),
    (r'口袋\s*(\d+)', r'第 \1 层'),
    (r'第\s*([一二三四五六七八九十]+)\s*口袋', r'第 \1 层'),
    (r'口袋', '区域'),
    (r'\bFirst [Pp]ocket\b', '第 1 层'),
    (r'\bSecond [Pp]ocket\b', '第 2 层'),
    (r'\bThird [Pp]ocket\b', '第 3 层'),
    (r'\bFourth [Pp]ocket\b', '第 4 层'),
    (r'\bFifth [Pp]ocket\b', '第 5 层'),
    (r'\bSixth [Pp]ocket\b', '第 6 层'),
    (r'\bSingle [Pp]ocket\b', '单层区域'),
    (r'\b([Bb]onus) [Pp]ocket\b', '奖励区域'),
]


def fix_terms(text):
    """渲染后的术语统一（目前只有 pocket → 层/区域）。"""
    if not text:
        return text
    out = text
    for pattern, repl in POCKET_RULES:
        out = re.sub(pattern, repl, out)
    return out

def render(rec):
    """一条任务 → 中文 Markdown 正文。"""
    out = []

    meta = ['**分类** %s' % CATEGORY_CN.get(rec['Category'], '任务')]
    if rec['Level'] > 0:
        meta.append('**等级** %d' % rec['Level'])
    if rec['MissionType']:
        meta.append('**类型** %s' % cn_short(rec['MissionType']))
    if rec['Faction'] >= 0:
        meta.append('**势力** %s' % FACTION_CN.get(rec['Faction'], ''))
    if rec['Arc']:
        meta.append('**任务线** %s' % rec['Arc'])
    if rec.get('Agent'):
        meta.append('**代理人** %s' % rec['Agent'])
    out.append(' ｜ '.join(meta))
    out.append('')

    raw = rec.get('_Raw') or {}

    def val(field):
        """字段展示值：译文 → 短词词典 → SDE 物品名（`中文（English）`）。"""
        out2 = val_raw(field)
        if out2 and not CJK_HAS(out2):
            out2 = annotate_items(out2)
        return out2

    def val_raw(field):
        """字段的展示值：先用**原始值**查译文（译文键是原文算的），再清 HTML；
        仍是英文且是短词时退到本地词典（Cruiser → 巡洋舰）。"""
        source = raw.get(field) or rec.get(field) or ''
        out = clean_value(tr(source))
        if out and not re.search(r'[\u4e00-\u9fff]', out):
            out = cn_short(out)
        return out

    if rec['Objective']:
        out.append('### 任务目标')
        out.append('')
        out.append(val('Objective'))
        out.append('')

    attrs = []
    if rec['Ewar']:
        # WebPoint 与 EWAR 在 wiki 上是两个字段，译文也是分开存的，拼起来才不会漏
        parts = [val('WebPointRaw'), val('EWARRaw')]
        text = ' / '.join(p for p in parts if p)
        attrs.append('- **电子战 / 反跳点**：%s' % (text or val('Ewar')))
    if rec['ShipLimit']:
        attrs.append('- **船型限制**：%s' % val('ShipLimit'))
    if rec['ShipSuggestion']:
        attrs.append('- **推荐船型**：%s' % val('ShipSuggestion'))
    if rec['Rewards']:
        attrs.append('- **奖励**：%s' % val('Rewards'))
    if rec['StandingLoss']:
        attrs.append('- **声望损失**：%s' % val('StandingLoss'))
    if rec['Extra']:
        attrs.append('- **备注**：%s' % val('Extra'))
    if rec['Location']:
        attrs.append('- **地点**：%s' % rec['Location'])
    if attrs:
        out.append('### 属性')
        out.append('')
        out.extend(attrs)
        out.append('')



    if rec.get('Briefing'):
        out.append('### 简报')
        out.append('')
        out.append(tr_block(rec['Briefing']))
        out.append('')


    for sec in rec.get('Sections') or []:
        text = (sec.get('text') or '').strip()
        npcs = sec.get('npcs') or []
        if not text and not npcs:
            continue
        if sec.get('title'):
            out.append('### ' + section_title_cn(sec['title']))
            out.append('')
        if text:
            out.append(tr_block(text))
            out.append('')
        for table in npcs:
            md = npc_table_md(table)
            if md:
                out.append(md)

    body = fix_terms('\n'.join(out).strip())
    body += '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC_DATE

    if rec.get('Untranslated'):
        body = ('> 本页正文暂未翻译（wiki 原文）。\n\n' + body)
    return body


ALIGN = {}
# Category:COSMOS missions 的页面清单。这些页面在 wiki 上分类信息不全，抓取时全落成了
# "安全任务"；构建时按清单改成 COSMOS（界面显示「空中代理人」）。
COSMOS_FILE = os.path.join(HERE, 'cosmos_missions.json')
COSMOS_TITLES = set()
MissionCategoryCosmos = 6


def dedupe_names(records):
    """同一页多个变体（Human Cattle 有 5 个势力的版本）会重名，用势力/序号区分，否则列表里分不清。"""
    groups = {}
    for r in records:
        groups.setdefault(r['TitleCN'], []).append(r)

    for zh, items in groups.items():
        if len(items) < 2:
            continue
        used = set()
        levels = {r['Level'] for r in items}

        # 标题里带的势力词（`(Blood Raiders)` / `(Guristas Pirates)`）比 i/N 有用得多：
        # 否则会出现「…（8/10）（1/3）」这种两套数字堆一起的名字
        def faction_from_en(title):
            low = (title or '').lower()
            for token, cn in (('blood raider', '血袭者'), ('guristas', '古斯塔斯'),
                              ('sansha', '萨沙'), ('serpentis', '天蛇'), ('angel', '天使'),
                              ('mercenar', '雇佣兵'), ('amarr', '艾玛'), ('caldari', '加达里'),
                              ('gallente', '盖伦特'), ('minmatar', '米玛塔尔'),
                              ('ammatar', '艾玛达'), ('khanid', '卡尼德')):
                if token in low:
                    return cn
            return ''

        labels = []
        for r in items:
            fac = FACTION_CN.get(r['Faction'], '') if r['Faction'] >= 0 else ''
            labels.append(fac or faction_from_en(r.get('TitleEN')) or '')

        use_labels = all(labels) and len(set(labels)) == len(labels)

        for i, r in enumerate(items, 1):
            # 区分依据按"最能把它们分开的那个维度"来：
            # ① 同一天使少女的 L2/L3/L4 → 用等级；② Human Cattle 5 个势力版本 → 用势力；
            # ③ 同页多种类型（战斗/运输）→ 用类型；④ 都不行才用序号
            fac = FACTION_CN.get(r['Faction'], '') if r['Faction'] >= 0 else ''
            typ = TYPE_CN.get((r['MissionType'] or '').lower(), r['MissionType'] or '')
            if len(levels) == len(items):
                suffix = 'L%d' % r['Level']
            elif use_labels:
                suffix = labels[i - 1]
            else:
                # 同一任务的多个版本（Human Cattle 的 5 个势力版、Air Show! 的 5 段）用 1/N 最稳，
                # 用势力/类型会出现"无人机 / 运输 / 战斗5"这种半斤八两的标签
                suffix = '%d/%d' % (i, len(items))
            suffix = (suffix or '').strip() or '版本%d' % i
            if suffix in used:
                suffix = '%s%d' % (suffix, i)
            used.add(suffix)
            r['TitleCN'] = '%s（%s）' % (zh, suffix)

def build():
    pages = json.load(io.open(PARSED, encoding='utf-8'))
    global TRANS, ALIGN
    TRANS = json.load(io.open(TRANS, encoding='utf-8')) if os.path.exists(TRANS) else {}
    ALIGN = json.load(io.open(ALIGN_FILE, encoding='utf-8')) if os.path.exists(ALIGN_FILE) else {}
    global COSMOS_TITLES
    COSMOS_TITLES = ({t.lower() for t in json.load(io.open(COSMOS_FILE, encoding='utf-8'))}
                     if os.path.exists(COSMOS_FILE) else set())

    records = []
    rid = 0
    for page in pages:
        title = page['title']
        # 逐条核对时判定为"不是任务页"的（用户沙盒、任务线章节枢纽、系列总览页）不入库
        if page.get('excluded'):
            continue
        # 标题里带下划线的都是同一任务页的畸形副本（`Driving_a_Wedge_-_…`），wiki 正常标题用空格
        if '_' in title:
            continue
        variants = [v for v in page['variants'] if v.get('info')]
        if not variants:
            continue

        base_title, title_level = strip_suffix(title)
        arc = base_title.split('/')[0] if page['cat'] == 4 else (page.get('arcOf') or '')

        for idx, v in enumerate(variants):
            info = v['info']
            rid += 1
            # 代理人页（{{Infobox agent}}）的任务名在小节标题里，页面标题是代理人名
            vtitle = (v.get('missionTitle') or '').strip() or title

            level = 0
            raw_level = str(info.get('Level', '')).strip()
            if raw_level.isdigit():
                level = int(raw_level)
            elif title_level:
                level = title_level

            title_en = vtitle
            # 中文名优先级：SDE 官方译名 > 旧库社区名 > wiki 直译
            # 分段/等级后缀统一交给 dedupe_names() 处理（否则会叠两层：xxx（L2）（运输））
            if page.get('sdeZh') and not v.get('missionTitle'):
                title_cn = re.sub(r'\((\d+)\s*/\s*(\d+)\)', r'（\1/\2）', page['sdeZh'])
                # 同一 wiki 页里塞了多个变体（`Small Misunderstanding` 有 3 段）时，
                # SDE 名字里的分段号会和 dedupe 的 1/N 打架 → 去掉分段号，统一由 dedupe 生成
                if len(variants) > 1:
                    title_cn = re.sub(r'[（(]\s*\d+\s*/\s*\d+\s*[）)]\s*$', '', title_cn).strip()
            elif vtitle in ALIGN:
                title_cn = ALIGN[vtitle]
            else:
                # 普通页面：先查整页标题，再退到去掉「(Level N)」的页名；
                # 代理人页（vtitle 是小节标题）不能再退到页名——那会变成"代理人名（1/4）"
                if vtitle != title:
                    title_cn = tr(vtitle)
                else:
                    title_cn = tr(title) if tr(title) != title else tr(base_title)

            # SDE 里同一英文名可能挂两套中文（"警方介入 / 警方侵略"），
            # 之前把它们用 " / " 拼成了中文名（截图里就是这种重复），这里只留第一套
            if ' / ' in title_cn:
                title_cn = title_cn.split(' / ')[0].strip()
            # 正文里偶尔能捞到代理人/空间站，作为"地点"
            location = ''
            for sec in v.get('sections') or []:
                m = re.search(r'([A-Z0-9]{2,7}\s+[IVX]+\s*-\s*Moon\s*\d+\s*-\s*[^\n\.]+)', sec.get('text') or '')
                if m:
                    location = m.group(1).strip()
                    break
            # 页面开头的 `Agent location:` 更可靠（COSMOS 页都有）
            if not location:
                location = page.get('agentLocation') or ''

            sections = []
            for sec in v.get('sections') or []:
                sections.append({
                    'title': sec.get('title') or '',
                    'text': sec.get('text') or '',
                    'npcs': sec.get('npcs') or [],
                })

            rec = {
                'Id': rid,
                # COSMOS 任务按清单归类（wiki 分类不全，否则会混进"安全任务"）
                'Category': MissionCategoryCosmos if title.lower() in COSMOS_TITLES else page['cat'],
                'Level': level,
                'MissionType': clean_value(str(info.get('Type', ''))),
                'Faction': faction_of(info),
                'TitleEN': vtitle,
                'TitleCN': title_cn,
                # 这 7 个字段是**展示字段**（页面属性区、AI 工具、搜索都读它们），
                # 必须存译文而不是原文——之前存的是清理后的英文，导致工具与搜索只认英文
                'Objective': clean_value(tr_raw(info.get('Objective', ''))),
                'Ewar': ' / '.join(x for x in [
                    clean_value(tr_raw(info.get('WebPoint', ''))),
                    clean_value(tr_raw(info.get('EWAR', '')))] if x),
                # 这三列经常就是"舰船/物品清单"，没有译文时用 SDE 官方名补成 `中文（English）`
                'ShipLimit': item_value(info.get('ShipSizeLimit', '')),
                'ShipSuggestion': item_value(info.get('ShipSuggestion', '')),
                'Rewards': item_value(info.get('Rewards', '')),
                'StandingLoss': clean_value(tr_raw(info.get('StandingLoss', ''))),
                'Extra': clean_value(tr_raw(info.get('Extra', ''))),
                'Location': location,
                'Arc': arc,
                'Agent': page.get('agent') or '',
                'Variant': '',   # 分段后缀统一由 dedupe_names() 生成
                'WikiTitle': title,
                # 简报：SDE 的官方中文优先；没有就用 wiki 的 {{MissionBriefing}}
                # （正文段落已按段送翻，tr_paragraphs 逐段查译文）
                'Briefing': page.get('sdeBriefing') or tr_paragraphs(v.get('briefing') or ''),
                'Sections': sections,
                '_Raw': {
                    'Objective': str(info.get('Objective', '')).strip(),
                    'Ewar': ' / '.join(x for x in [
                        str(info.get('WebPoint', '')).strip(),
                        str(info.get('EWAR', '')).strip()] if x),
                    'WebPointRaw': str(info.get('WebPoint', '')).strip(),
                    'EWARRaw': str(info.get('EWAR', '')).strip(),
                    'ShipLimit': str(info.get('ShipSizeLimit', '')).strip(),
                    'ShipSuggestion': str(info.get('ShipSuggestion', '')).strip(),
                    'Rewards': str(info.get('Rewards', '')).strip(),
                    'StandingLoss': str(info.get('StandingLoss', '')).strip(),
                    'Extra': str(info.get('Extra', '')).strip(),
                },
                'IsStub': 0,
                'WikiUpdatedAt': SYNC_DATE,
            }
            rec['WikiContent'] = render(rec)
            if len(rec['WikiContent']) < 400:
                rec['IsStub'] = 1
            records.append(rec)

    # 追加「没有 {{Missiondetails}} 的 COSMOS 任务页」（cosmos_extra.json，由
    # fetch_cosmos_missions.py 解析）。这些页的元信息在开头 `Agent: / Agent location:` 行里。
    known_titles = {r['TitleEN'].lower() for r in records}
    added = 0
    for extra_name in ('cosmos_extra.json', 'storyline_extra.json'):
        extra_file = os.path.join(HERE, extra_name)
        if not os.path.exists(extra_file):
            continue
        for item in json.load(io.open(extra_file, encoding='utf-8')):
            if item['title'].lower() in known_titles:
                continue
            meta = item.get('meta') or {}
            sections = item.get('sections') or []
            if not sections and not item.get('briefing'):
                continue
            rid += 1
            rec = {
                'Id': rid,
                'Agent': meta.get('agent') or '',
                'Category': item.get('cat', MissionCategoryCosmos),
                'Level': 0,
                'MissionType': '',
                'Faction': faction_of(meta),
                'TitleEN': item['title'],
                'TitleCN': tr(item['title']) if tr(item['title']) != item['title'] else item['title'],
                'Objective': '',
                'Ewar': '',
                'ShipLimit': '',
                'ShipSuggestion': '',
                'Rewards': '',
                'StandingLoss': '',
                'Extra': meta.get('standing') or '',
                'Location': meta.get('location') or '',
                'Arc': meta.get('cosmos') or '',
                'Variant': '',
                'WikiTitle': item['title'],
                'Briefing': tr_paragraphs(item.get('briefing') or ''),
                'Sections': sections,
                'IsStub': False,
                'WikiUpdatedAt': SYNC_DATE,
                '_Raw': {},
            }
            rec['WikiContent'] = render(rec)
            records.append(rec)
            known_titles.add(item['title'].lower())
            added += 1
    if added:
        print('  追加补充任务 %d 条' % added)

    dedupe_names(records)

    # 不同 wiki 页仍可能指向同一任务（`Mordus Folly` 与 `Mordu's Folly`、`Small Misunderstanding`
    # 与 `Small Misunderstanding (1 of 3)`），中文名撞车就只留正文最长的一条
    best = {}
    for r in records:
        key = r['TitleCN']
        if key not in best or len(r.get('WikiContent') or '') > len(best[key].get('WikiContent') or ''):
            best[key] = r
    records = [r for r in records if best[r['TitleCN']] is r]

    json.dump(records, io.open(OUT_JSON, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

    by_cat = {}
    for r in records:
        by_cat[r['Category']] = by_cat.get(r['Category'], 0) + 1
    print('生成 %d 条任务：' % len(records))
    for c in sorted(by_cat):
        print('  %-14s %3d' % (CATEGORY_CN[c], by_cat[c]))
    print('  stub（正文 < 400 字）：%d' % sum(1 for r in records if r['IsStub']))
    print('  带任务目标：%d ｜ 带奖励：%d ｜ 有 NPC 表：%d'
          % (sum(1 for r in records if r['Objective']),
             sum(1 for r in records if r['Rewards']),
             sum(1 for r in records if any(s['npcs'] for s in r['Sections']))))
    return records


SCHEMA = '''
CREATE TABLE Mission (
    Id INTEGER PRIMARY KEY,
    Agent TEXT,
    Category INTEGER NOT NULL DEFAULT 0,
    Level INTEGER NOT NULL DEFAULT 0,
    MissionType TEXT,
    Faction INTEGER NOT NULL DEFAULT -1,
    TitleEN TEXT,
    TitleCN TEXT,
    Objective TEXT,
    Ewar TEXT,
    ShipLimit TEXT,
    ShipSuggestion TEXT,
    Rewards TEXT,
    StandingLoss TEXT,
    Extra TEXT,
    Location TEXT,
    Arc TEXT,
    Variant TEXT,
    WikiTitle TEXT,
    WikiContent TEXT,
    IsStub INTEGER NOT NULL DEFAULT 0,
    WikiUpdatedAt TEXT
)'''


def write_db(records):
    con = sqlite3.connect(STATIC_DB)
    cur = con.cursor()
    cur.execute('DROP TABLE IF EXISTS Mission')
    cur.execute(SCHEMA)
    # 老"任务正文"表一并退役（内容已由 WikiContent 覆盖）
    cur.execute('DROP TABLE IF EXISTS MissionContent')
    cur.executemany(
        'INSERT INTO Mission (Id, Agent, Category, Level, MissionType, Faction, TitleEN, TitleCN,'
        ' Objective, Ewar, ShipLimit, ShipSuggestion, Rewards, StandingLoss, Extra, Location,'
        ' Arc, Variant, WikiTitle, WikiContent, IsStub, WikiUpdatedAt)'
        ' VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)',
        [(r['Id'], r.get('Agent'), r['Category'], r['Level'], r['MissionType'], r['Faction'], r['TitleEN'],
          r['TitleCN'], r['Objective'], r['Ewar'], r['ShipLimit'], r['ShipSuggestion'],
          r['Rewards'], r['StandingLoss'], r['Extra'], r['Location'], r['Arc'], r['Variant'],
          r['WikiTitle'], r['WikiContent'], r['IsStub'], r['WikiUpdatedAt']) for r in records])
    cur.execute('CREATE INDEX IF NOT EXISTS IX_Mission_Category ON Mission(Category)')
    cur.execute('CREATE INDEX IF NOT EXISTS IX_Mission_Level ON Mission(Level)')
    con.commit()
    con.execute('VACUUM')
    con.commit()
    con.close()
    print('已写入 %s（%d 条）' % (STATIC_DB, len(records)))


def main():
    records = build()
    if '--write-db' in sys.argv:
        write_db(records)
    return 0


if __name__ == '__main__':
    sys.exit(main())
