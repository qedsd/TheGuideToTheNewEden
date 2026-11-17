"""把解析结果 + 中文译文组装成 `CombatSite` 记录，并写入 `static.db`。

流程
----
1. 读 `index.json`（站点清单：分类 / 势力 / 评级 / 档位 / 安等 / 升级目标 / 触发来源）
2. 按 `page` 关联 `parsed.json`（wiki 页正文结构）
3. 用 `translations.json` / `translations_names.json` 回填中文
4. NPC 名 / 物品名 / 建筑名优先走本地 SDE（`main.db` + `Local/zh.db`）的中文名
5. 渲染成中文 Markdown（`WikiContent`）
6. 把原 `DED` 表的 192 条按英文标题迁移成 `CommunityContent`，并补进 8 条新站点
7. 建表 + 写入 `Resources/Database/static.db`

用法
----
    python build_sites.py            # 只生成 sites.json（可先看效果）
    python build_sites.py --write-db # 同时写数据库
"""

import io
import json
import os
import re
import sqlite3
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
DB_DIR = os.path.join(ROOT, 'TheGuideToTheNewEden', 'TheGuideToTheNewEden.WPF', 'Resources', 'Database')
STATIC_DB = os.path.join(DB_DIR, 'static.db')
MAIN_DB = os.path.join(DB_DIR, 'main.db')
ZH_DB = os.path.join(DB_DIR, 'Local', 'zh.db')

FACTIONS = {0: '无人机', 1: '天蛇', 2: '血袭者', 3: '萨沙', 4: '天使', 5: '古斯塔斯'}
FACTION_EN = {
    'Rogue Drones': 0, 'Serpentis Corporation': 1, 'Serpentis': 1,
    'Blood Raiders': 2, 'The Blood Raider Covenant': 2, 'Blood Raider Covenant': 2,
    "Sansha's Nation": 3, 'Sansha': 3, 'Angel Cartel': 4, 'Angels': 4, 'Angel': 4,
    'Guristas Pirates': 5, 'Guristas': 5, 'Gurista': 5,
}

CATEGORY_CN = {
    0: 'DED 评级复合体', 1: '无评级复合体', 2: '远征', 3: '战斗异常',
    4: '化学实验室', 5: '战斗遗迹', 6: '三神裔站点', 7: '特殊站点', 8: '旗舰远征',
}

CLASS_CN = {
    'frigate': '护卫舰', 'elite frigate': '精英护卫舰', 'commander frigate': '指挥官护卫舰',
    'destroyer': '驱逐舰', 'elite destroyer': '精英驱逐舰',
    'cruiser': '巡洋舰', 'elite cruiser': '精英巡洋舰', 'commander cruiser': '指挥官巡洋舰',
    'battlecruiser': '战巡', 'elite battlecruiser': '精英战巡',
    'battleship': '战列舰', 'elite battleship': '精英战列舰', 'commander battleship': '指挥官战列舰',
    'commander': '指挥官', 'capital': '旗舰', 'carrier': '航母', 'dreadnought': '无畏',
    'sentry': '岗哨炮塔', 'structure': '建筑', 'station': '空间站', 'hangar array': '机库阵列',
    'acceleration gate': '加速轨道', 'asteroid': '小行星', 'container': '集装箱',
    'drone': '无人机', 'elite drone': '精英无人机', 'wreck': '残骸', 'deadspace': '死亡空间',
    'stargate': '星门', 'sentry gun': '岗哨炮塔', 'battery': '炮塔',
}

SHIP_LIMIT_CN = {
    'frigate': '护卫舰', 'destroyer': '驱逐舰', 'cruiser': '巡洋舰（T3 除外）',
    'battlecruiser': '战巡（T3 巡洋除外）', 'battleship': '战列舰',
    't2 battleship': 'T2 战列舰', 't3 cruiser': 'T3 巡洋舰',
    'unrestricted': '无限制', 'none': '无限制', '': '',
}

# wiki 的 `ship limit` 常见写法不统一（单数/复数/T2 前缀），先归一化再查表
def ship_limit_cn(text):
    raw = (text or '').strip()
    if not raw:
        return ''
    key = raw.lower().rstrip('.').strip()
    if key in SHIP_LIMIT_CN:
        return SHIP_LIMIT_CN[key]
    if key.endswith('s') and key[:-1] in SHIP_LIMIT_CN:
        return SHIP_LIMIT_CN[key[:-1]]
    return raw


# 电子战缩写 → 中文（NPC 表 `ewar=` 字段）
EWAR_CN = {
    'web': '网子', 'webifier': '网子', 'stasis web': '网子', 'stasis webifier': '网子',
    'ecm': 'ECM', 'jam': 'ECM', 'jamming': 'ECM',
    'damp': '感应抑阻', 'dampener': '感应抑阻', 'sensor damp': '感应抑阻',
    'sensor dampener': '感应抑阻', 'remote sensor dampener': '远程感应抑阻',
    'td': '索敌扰断', 'tracking disruptor': '索敌扰断', 'tracking disruption': '索敌扰断',
    'tp': '目标标记', 'target painter': '目标标记',
    'neut': '毁电', 'neutralizer': '毁电', 'nos': '吸电', 'energy neutralizer': '毁电',
    'point': '反跳', 'scram': '反跳', 'warp disruptor': '反跳', 'warp scrambler': '反跳',
    'webbing': '网子',
}


def ewar_cn(text):
    raw = (text or '').strip()
    if not raw:
        return ''
    low = raw.lower().rstrip('.')
    if low in EWAR_CN:
        return EWAR_CN[low]
    # "Web, ECM" 这种多值写法
    parts = [p.strip() for p in re.split(r'[,/&]', raw) if p.strip()]
    mapped = [EWAR_CN.get(p.lower().rstrip('.'), None) for p in parts]
    if parts and all(mapped):
        return '、'.join(mapped)
    return tr(raw)

SEC_CN = {1: '高安', 2: '低安', 4: '00'}

RELIC_REGION = {
    'Ancient Ruins': 'Catch（9HXQ-G 星座）',
    'Bloated Ruins': 'Wicked Creek（760-9C 星座）',
    'Crumbling Ruins': 'Vale of the Silent',
    'Festering Ruins': 'Fountain',
    'Forgotten Ruins': 'Delve',
    'Hidden Ruins': 'Cloud Ring',
    'Wispy Ruins': 'Feythabolis（I-3ODK 星座）',
}

# wiki 索引里的页名与实际抓下来的文件名不一致时的人工别名
PAGE_ALIAS = {
    'Besieged_Covert_Research_Factory': 'Besieged_Covert_Research_Facility',
}

# 站点中文名的人工覆盖（只兜"红链站点"——wiki 没页面、SDE 也没名字的那几条）
NAME_OVERRIDE_BY_EN = {
    'Angel Forsaken Sanctum': '天使离弃圣所',
    'Blood Forsaken Sanctum': '血袭者离弃圣所',
    'Guristas Forsaken Sanctum': '古斯塔斯离弃圣所',
    'Teeming Drone Horde': '蜂拥无人机组群',
    'Blood Raider Gas Processing Site': '血袭者气体处理站',
    'H-PA Crew Crash Distribution Base': 'H-PA 船员“撞击”分销基地',
    'H-PA Crew Blue Pill Production Facility': 'H-PA 船员“蓝色药丸”生产设施',
    'Sansha Chemical Lab': '萨沙化学实验室',
}

# 同英文名的两条（5/10 与 10/10 的无人机母巢），只能按 wiki 页区分
NAME_OVERRIDE_BY_PAGE = {
    'Outgrowth_Rogue_Drone_Hive_(5_10)': '扩张自由无人机母巢（5/10）',
    'Outgrowth_Rogue_Drone_Hive_(5/10)': '扩张自由无人机母巢（5/10）',
    'Outgrowth_Rogue_Drone_Hive_(10_10)': '扩张自由无人机母巢（10/10）',
    'Outgrowth_Rogue_Drone_Hive_(10/10)': '扩张自由无人机母巢（10/10）',
}

NO_PAGE_NOTE = ('> 该站点在 EVE University Wiki 上暂无独立页面（红链），这里只有基本信息；'
                '游戏内实测数据可自行补充到「社区攻略」。')

# 旗舰远征的已知触发关系（wiki 只在个别异常的"可升级为"里提过，其余没有记载）
CAPITAL_SOURCE = {
    'Serpentis Capital Staging': '天蛇 · 隐秘集会点（Serpentis Hidden Rally Point）',
    'Serpentis Shielded Starbase': '天蛇 · 中枢（Serpentis Hub，wiki 标注为 8/10 护盾加强母星）',
    'Drone Infested Starbase': '无人机 · 组群（Drone Herd）',
}

# 上表的机器可读版：旗舰英文名 → 来源异常英文名（用于给异常挂 EscalationId）
CAPITAL_SOURCE_SITE = {
    'Serpentis Capital Staging': 'Serpentis Hidden Rally Point',
    'Serpentis Shielded Starbase': 'Serpentis Hub',
    'Drone Infested Starbase': 'Drone Herd',
}

CAPITAL_KIND_CN = {
    'staging': '旗舰集结地',
    'hive': '旗舰机巢',
    'starbase': '护盾加强母星',
    'infested': '被侵袭的母星',
}

# SDE 的 factionID → 本项目势力索引（与 extract_sde_dungeons.py 保持一致）
SDE_FACTION_ID = {0: 500021, 1: 500020, 2: 500012, 3: 500019, 4: 500011, 5: 500010}


def official_name(en, faction):
    """按 SDE 的「英文名 → 官方中文名」查名字。

    同一个英文名可能对应多条 dungeon（不同安等/等级的同型站点，中文名偶尔不一致），
    优先取**同势力**那条，其次取出现次数最多的那个。
    """
    if not en:
        return ''
    cands = SDE_NAMES.get(en.strip())
    if not cands:
        # 大小写不敏感兜底
        low = en.strip().lower()
        for key, value in SDE_NAMES.items():
            if key.lower() == low:
                cands = value
                break
    if not cands:
        return ''
    if faction is not None and faction >= 0:
        fid = SDE_FACTION_ID.get(faction)
        for c in cands:
            if c.get('factionId') == fid:
                return c.get('zh') or ''
    tally = {}
    for c in cands:
        z = c.get('zh') or ''
        if z:
            tally[z] = tally.get(z, 0) + 1
    if not tally:
        return ''
    return max(tally.items(), key=lambda kv: kv[1])[0]


def disambiguate(records):
    """官方中文名会撞车（例：`Sansha Military Complex` 与 `…Operations Complex` 官方都叫"沙沙军用复合体"）。

    撞车时给**非首条**加语义后缀（评级 / 档位 / 类别），仍然撞就退化成 `#Id`，保证列表里能区分。
    """
    groups = {}
    for r in records:
        groups.setdefault(r['TitleCN'], []).append(r)

    def suffix_of(r):
        cat = r['Category']
        if cat == 3:
            return 'T%d·%d' % (r['Tier'], r['Level']) if r['Level'] else 'T%d' % r['Tier']
        if cat == 0:
            return '%d/10' % r['Rating']
        if cat == 1:
            return '%d/10' % r['Rating'] if r['Rating'] > 0 else '无评级'
        if cat == 8:
            return '旗舰远征'
        if cat == 2:
            return '远征'
        return ''

    renamed = []
    for zh, items in groups.items():
        if len(items) < 2:
            continue
        # 先给能拿到语义后缀的加后缀；后缀为空的放最后再兜底
        for r in items:
            sfx = suffix_of(r)
            r['TitleCN'] = '%s（%s）' % (zh, sfx) if sfx else zh
            renamed.append((zh, r['TitleCN'], r['TitleEN']))

    # 第二轮：加完后缀还撞的，用 Id 兜底
    again = {}
    for r in records:
        again.setdefault(r['TitleCN'], []).append(r)
    for zh, items in again.items():
        if len(items) > 1:
            for r in items:
                r['TitleCN'] = '%s #%d' % (zh, r['Id'])
                renamed.append((zh, r['TitleCN'], r['TitleEN']))

    return renamed

# 同名的两条（5/10 与 10/10）在英文标题上也要带评级，否则列表里无法区分
EN_OVERRIDE_BY_PAGE = {
    'Outgrowth_Rogue_Drone_Hive_(5_10)': 'Outgrowth Rogue Drone Hive (5/10)',
    'Outgrowth_Rogue_Drone_Hive_(5/10)': 'Outgrowth Rogue Drone Hive (5/10)',
    'Outgrowth_Rogue_Drone_Hive_(10_10)': 'Outgrowth Rogue Drone Hive (10/10)',
    'Outgrowth_Rogue_Drone_Hive_(10/10)': 'Outgrowth Rogue Drone Hive (10/10)',
}


def load_json(name, default=None):
    p = os.path.join(HERE, name)
    if not os.path.exists(p):
        return default
    return json.load(io.open(p, encoding='utf-8'))


# ---------------------------------------------------------------- SDE 名词
class Sde:
    def __init__(self):
        self.cache = {}
        self.by_name = {}
        self.by_zh = {}
        if os.path.exists(MAIN_DB) and os.path.exists(ZH_DB):
            mn = sqlite3.connect('file:%s?mode=ro' % MAIN_DB, uri=True)
            zh = sqlite3.connect('file:%s?mode=ro' % ZH_DB, uri=True)
            for tid, name in mn.execute('select Id, Name from types'):
                self.by_name[name.lower()] = tid
            for tid, name in zh.execute('select Id, Name from types'):
                self.by_zh[tid] = name
            mn.close()
            zh.close()
            self.ready = True
        else:
            self.ready = False

    def raw(self, name):
        key = name.strip().lower()
        if key in self.cache:
            return self.cache[key]
        v = None
        tid = self.by_name.get(key)
        if tid is not None:
            v = self.by_zh.get(tid)
        self.cache[key] = v
        return v

    def group(self, name):
        """`Centior Abomination/Horror` 这类共享前缀的写法，逐个推出完整名再查中文。"""
        name = (name or '').strip()
        if not name:
            return ''
        parts = [p.strip() for p in name.split('/') if p.strip()]
        if not parts:
            return ''
        first_words = parts[0].split()
        prefix = ' '.join(first_words[:-1]) if len(first_words) > 1 else ''
        out = []
        for i, p in enumerate(parts):
            got = self._try(p)
            if not got and i > 0 and prefix:
                got = self._try(prefix + ' ' + p)
            out.append(got or p)
        return '/'.join(out)

    def _try(self, name):
        """整名先查；查不到再逐步剥掉 `Commander` / `Elite` / `Sentient` 这类前缀。"""
        got = self.raw(name)
        if got:
            return got
        words = name.split()
        for i in range(1, min(4, len(words))):
            got = self.raw(' '.join(words[i:]))
            if got:
                return got
        return None


SDE = Sde()

# 区域名（化学实验室用）
REGION_LINES = None


def region_lines():
    global REGION_LINES
    if REGION_LINES is not None:
        return REGION_LINES
    REGION_LINES = {}
    if os.path.exists(MAIN_DB) and os.path.exists(ZH_DB):
        mn = sqlite3.connect('file:%s?mode=ro' % MAIN_DB, uri=True)
        zh = sqlite3.connect('file:%s?mode=ro' % ZH_DB, uri=True)
        zmap = dict(zh.execute('select Id, Name from mapRegions'))
        for rid, name in mn.execute('select Id, Name from mapRegions'):
            if rid in zmap:
                REGION_LINES[name.lower()] = zmap[rid]
        mn.close()
        zh.close()
    return REGION_LINES


def region_cn(text):
    """`Heimatar, Molden Heath` -> `赫玛塔 / 摩登赫斯`"""
    if not text:
        return ''
    m = region_lines()
    out = []
    for part in re.split(r'[,，]', text):
        p = part.strip()
        if not p or p == '?':
            continue
        out.append(m.get(p.lower(), p))
    return ' / '.join(out)


# ---------------------------------------------------------------- 渲染
def tr(text):
    """查译文；查不到就保留英文原文（允许分批补齐）。"""
    if not text:
        return ''
    t = text.strip()
    return TRANS.get(hash_id(t), t)


def hash_id(text):
    import hashlib
    return hashlib.sha1(text.encode('utf-8')).hexdigest()[:12]


TRANS = {}
NAMES = {}
SDE_NAMES = {}


def name_cn(en):
    if not en:
        return ''
    en = en.strip()
    return NAMES.get(en, en)


def class_cn(cls):
    c = (cls or '').strip().lower()
    if not c:
        return ''
    if c in CLASS_CN:
        return CLASS_CN[c]
    return tr(cls)


def row_note(row):
    bits = []
    if row.get('point'):
        bits.append('反跳')
    if row.get('ewar'):
        bits.append('电子战：' + ewar_cn(row['ewar']))
    if row.get('trigger'):
        bits.append('触发：' + tr(row['trigger']))
    if row.get('note'):
        bits.append(tr(row['note']))
    if row.get('cargo'):
        bits.append('掉落：' + tr(row['cargo']))
    return '；'.join(b for b in bits if b)


def md_cell(x):
    x = (x or '').replace('|', '／').replace('\n', ' ').strip()
    return x or '—'


def count_cell(x):
    x = (x or '').strip()
    return x if x else '?'


def post_clean(text):
    """渲染前的最后一道清洗：去掉译文里残留的 wiki 外链与孤立符号行。"""
    t = re.sub(r'\[https?://[^\s\]]+\s*([^\]]*)\]', r'\1', text or '')
    t = re.sub(r'https?://\S+', '', t)
    lines = [ln for ln in t.split('\n') if ln.strip() not in ('*', '-', '|}', '{{', '}}', '|')]
    t = '\n'.join(lines)
    t = re.sub(r'\n{3,}', '\n\n', t)
    return t.strip()


def render_meta(rec):
    bits = []
    if rec['Category'] == 3 and rec['Tier']:
        t = '难度档 %d' % rec['Tier']
        if rec['Level']:
            t += ' · 第 %d 级' % rec['Level']
        bits.append(t)
    elif rec['Rating']:
        bits.append('评级 %d/10' % rec['Rating'])
    if rec['ShipLimit']:
        bits.append('最大可进船型 ' + rec['ShipLimit'])
    if rec['Location']:
        bits.append('出现空间 ' + rec['Location'])
    return ' ｜ '.join(bits)


def render_content(rec, page):
    """把一页 wiki 内容按原文章节顺序渲染成中文 Markdown。"""
    if not page:
        return ''
    out = []
    meta = render_meta(rec)
    if meta:
        out.append(meta)
        out.append('')

    for s in page['intro']:
        t = tr(s)
        if t:
            out.append(post_clean(t))
            out.append('')

    # 按 wiki 章节原始顺序输出（pocket / 说明段 / 战利品 / 升级）
    items = []
    for pocket in page['pockets']:
        items.append((pocket.get('order', 999), 'pocket', pocket))
    for sec in page['notes']:
        items.append((sec.get('order', 999), 'note', sec))
    for sec in page['rewards']:
        items.append((sec.get('order', 999), 'reward', sec))
    for sec in page['escalation']:
        items.append((sec.get('order', 999), 'escalation', sec))
    items.sort(key=lambda x: (x[0], x[1]))

    for _, kind, obj in items:
        if kind == 'pocket':
            title = tr(obj['title']) if obj['title'] else ''
            if title:
                out.append('### ' + title)
                out.append('')
            if obj['prose']:
                out.append(post_clean(tr(obj['prose'])))
                out.append('')
            for block in obj['blocks']:
                btitle = tr(block['title']) if block.get('title') else ''
                if btitle:
                    out.append('**%s**' % btitle)
                    out.append('')
                rows = [r for r in block['rows'] if r.get('separator') is None]
                if rows:
                    out.append('| 数量 | 吨位 | 名称 | 备注 |')
                    out.append('|---|---|---|---|')
                    for r in rows:
                        out.append('| %s | %s | %s | %s |' % (
                            count_cell(r.get('count')),
                            md_cell(class_cn(r.get('cls'))),
                            md_cell(SDE.group(r.get('name')) if r.get('name') else ''),
                            md_cell(row_note(r))))
                    out.append('')
        elif kind in ('note', 'reward', 'escalation'):
            title = obj['title']
            if kind == 'note' and title.strip().lower() in ('see also', 'references'):
                continue
            t = tr(obj['text'])
            if not t:
                continue
            if title:
                out.append('### ' + tr(title))
                out.append('')
            out.append(post_clean(t))
            out.append('')

    if page.get('lootRows'):
        out.append('### 战利品')
        out.append('')
        out.append('| 物品 | 估值 | 掉落来源 | 备注 |')
        out.append('|---|---|---|---|')
        for row in page['lootRows']:
            cells = (row + ['', '', '', ''])[:4]
            out.append('| %s | %s | %s | %s |' % (
                md_cell(SDE.group(cells[0]) if cells[0] else ''),
                md_cell(cells[1]),
                md_cell(SDE.group(cells[2]) if cells[2] else ''),
                md_cell(tr(cells[3]) if cells[3] else '')))
        out.append('')

    if rec['Source']:
        out.append('### 触发来源')
        out.append('')
        out.append(rec['Source'])
        out.append('')

    if rec['Escalation']:
        out.append('### 可升级为')
        out.append('')
        out.append(rec['Escalation'])
        out.append('')

    body = '\n'.join(out).strip()
    if body:
        body += '\n\n> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC_DATE
    return body


SYNC_DATE = time.strftime('%Y-%m-%d')


def render_capital(cap):
    """旗舰远征的正文：官方名 + 官方描述 + 触发关系 + 说明。wiki 没有这些条目，所以不假装有怪表。"""
    faction = FACTIONS.get(cap.get('faction', -1), '')
    kind = CAPITAL_KIND_CN.get(cap.get('kind', ''), '旗舰远征')
    out = []
    meta = ['**类别** 旗舰远征', '**类型** %s' % kind]
    if faction:
        meta.append('**势力** %s' % faction)
    if cap.get('descZh') and '旗舰' in cap['descZh']:
        meta.append('**建议** 旗舰')
    out.append(' ｜ '.join(meta))
    out.append('')

    desc = cap.get('descZh') or cap.get('descEn') or ''
    if desc:
        out.append(desc)
        out.append('')

    out.append('### 触发来源')
    out.append('')
    src = CAPITAL_SOURCE.get(cap['en'], '')
    out.append(src if src else 'EVE University Wiki 未记载该站点的具体触发点（同类站点由 00 地区的高等级异常升级而来）。'
                              '如果你实测出来了，欢迎补到「社区攻略」。')
    out.append('')

    out.append('### 说明')
    out.append('')
    out.append('- 这是**旗舰级远征**：官方描述明确建议用**旗舰**去打，普通战列舰很吃力。')
    out.append('- 暂无详细攻略。')
    out.append('')
    out.append('> 数据来自 EVE University Wiki（CC BY-SA 4.0），同步于 %s。' % SYNC_DATE)
    return '\n'.join(out)


# ---------------------------------------------------------------- 组装
SOURCE_FACTION_CN = {0: '无人机', 1: '天蛇', 2: '血袭者', 3: '萨沙', 4: '天使', 5: '古斯塔斯'}


def source_cn(src, faction):
    """`<Faction> Hideout Haunted Yard` -> `天使藏身处（无人机：Haunted Yard）`"""
    if not src:
        return ''
    f = SOURCE_FACTION_CN.get(faction, '')
    s = src.strip()
    alias = {'Provisional <Faction': '临时', 'Minor <Faction': '小型'}
    words = {
        'Hideout': '藏身处', 'Lookout': '哨所', 'Watch': '嘹望哨', 'Vigil': '警卫哨',
        'Outpost': '哨站', 'Annex': '附属区', 'Base': '基地', 'Fortress': '堡垒',
        'Military Complex': '军事复合体', 'Provincial HQ': '辖区总部',
        'Fleet Staging Point': '舰队集结点', 'DED 6/10': '6/10 续篇',
        'Provisional <Faction> Outpost': '临时哨站', 'Minor <Faction> Annex': '小型附属区',
    }
    for k in ('Provisional <Faction> Outpost', 'Minor <Faction> Annex', 'Military Complex',
              'Provincial HQ', 'Fleet Staging Point', 'DED 6/10', 'Hideout', 'Lookout',
              'Watch', 'Vigil', 'Outpost', 'Annex', 'Base', 'Fortress'):
        if s.startswith(k):
            rest = s[len(k):].strip()
            head = words.get(k, k)
            if f and '<Faction>' in k:
                head = f + head
            elif '<Faction>' in k:
                head = head.replace('<Faction>', f)
            suffix = ('（无人机：%s）' % rest) if rest and rest.lower() != 'haunted yard' else ''
            if rest.lower() == 'haunted yard':
                suffix = '（无人机：Haunted Yard）'
            return head + suffix
    return s


def security_bits(high, low, null):
    v = (1 if high else 0) | (2 if low else 0) | (4 if null else 0)
    return v


def sec_from_page(loc):
    """把 CMBSiteInfo 的 `location` 字段（如 `High, Low` / `?, ?, Null`）转成位掩码；判不出来返回 0。"""
    if not loc:
        return 0
    t = loc.lower()
    v = 0
    if 'high' in t:
        v |= 1
    if 'low' in t:
        v |= 2
    if 'null' in t or '0.0' in t:
        v |= 4
    return v


def security_cn(bits):
    return ' / '.join(SEC_CN[b] for b in (1, 2, 4) if bits & b)


def build():
    idx = load_json('index.json')
    parsed = load_json('parsed.json')
    global TRANS, NAMES, SDE_NAMES
    TRANS = load_json('translations.json', {})
    NAMES = load_json('translations_names.json', {})
    SDE_NAMES = load_json('sde_names.json', {})
    if not SDE_NAMES:
        print('  [提示] 没有 sde_names.json，中文名将沿用译文/旧库（先跑 extract_sde_dungeons.py 可用官方名）')

    # 现有 DED 表：英文标题 -> 记录（迁移社区攻略与既有中文名，保持 Id 稳定）
    old = []
    if os.path.exists(STATIC_DB):
        con = sqlite3.connect('file:%s?mode=ro' % STATIC_DB, uri=True)
        for row in con.execute('select Id, Type, Level, TitleCN, TitleEN, Content from DED'):
            old.append({'Id': row[0], 'Faction': row[1], 'Level': row[2],
                        'TitleCN': row[3] or '', 'TitleEN': row[4] or '', 'Content': row[5] or ''})
        con.close()

    def norm(x):
        x = (x or '').lower().replace('’', "'")
        x = re.sub(r'\(\d+/\d+\)', '', x)
        x = re.sub(r'[^a-z0-9]+', ' ', x)
        x = re.sub(r'\bguristas\b', 'gurista', x)
        x = re.sub(r'\bfrentex\b', 'frentix', x)
        return ' '.join(x.split())

    old_by = {}
    for o in old:
        old_by.setdefault(norm(o['TitleEN']), o)

    # wiki 页名与实际抓下来的文件名可能不一致：下划线/空格、重定向后的显示名、
    # 甚至拼写差异（`Pith_Robux_Asteroid_Mining_&_Co.` vs `Pith-Robux Asteroid Mining Co.`）。
    # 这里用"去掉全部非字母数字后的小写"做规范键，能一次覆盖上述几种情况。
    def canon(k):
        return re.sub(r'[^a-z0-9]+', '', (k or '').lower())

    parsed_canon = {}
    for k in parsed:
        parsed_canon.setdefault(canon(k), k)

    def get_page(key):
        if not key:
            return None
        key = PAGE_ALIAS.get(key, key)
        if key in parsed:
            return parsed[key]
        real = parsed_canon.get(canon(key))
        return parsed.get(real) if real else None

    # wiki 页名 -> 中文站名（升级目标要用它把 "4/10" 说成"古斯塔斯侦察哨站（4/10）"）
    PAGE_CN = {}
    for bucket, items in idx.items():
        if not isinstance(items, list):
            continue
        for it in items:
            page, site = it.get('page'), it.get('site')
            if not page or not site:
                continue
            o = old_by.get(norm(site))
            cn = (o['TitleCN'] if o and o['TitleCN'] else '') or NAMES.get(site, '')
            if cn:
                PAGE_CN.setdefault(page, cn)

    used_ids = set()
    next_id = max([o['Id'] for o in old], default=0) + 1
    records = []

    def add(category, faction, rating, tier, level, security, ship_limit, title_en,
            escalation='', source='', location='', page_key=None, force_stub=False):
        nonlocal next_id
        o = old_by.get(norm(title_en))
        rid = None
        if o and o['Id'] not in used_ids:
            rid = o['Id']
        else:
            while next_id in used_ids:
                next_id += 1
            rid = next_id
            next_id += 1
        used_ids.add(rid)

        # 名字优先级：人工按页覆盖（同英文名分 5/10 与 10/10 那种）
        #            → **游戏官方中文名（SDE）**：与客户端显示一致，优先于社区旧译名
        #            → 红链站点的人工名
        #            → 旧 DED 库里的社区译名
        #            → 站点名译文
        title_cn = (NAME_OVERRIDE_BY_PAGE.get(page_key or '', '')
                    or official_name(title_en, faction)
                    or NAME_OVERRIDE_BY_EN.get(title_en, '')
                    or (o['TitleCN'] if o and o['TitleCN'] else '')
                    or name_cn(title_en))
        community = (o['Content'] if o else '') or ''
        page = get_page(page_key)
        rec = {
            'Id': rid,
            'Category': category,
            'Faction': faction,
            'Rating': rating,
            'Tier': tier,
            'Level': level,
            'Security': security,
            'ShipLimit': ship_limit,
            'TitleCN': title_cn,
            'TitleEN': EN_OVERRIDE_BY_PAGE.get(page_key or '', '')
                       or (o['TitleEN'] if o and o['TitleEN'] else title_en).strip(),
            'Escalation': escalation,
            'EscalationId': 0,
            'Source': source,
            'Location': location,
            'WikiTitle': page_key or '',
            'CommunityContent': community,
            'WikiContent': '',
            'IsStub': 1 if (force_stub or (page and page['isStub'])) else 0,
            'CommunityUpdatedAt': '',
            'WikiUpdatedAt': SYNC_DATE if page else '',
        }
        rec['WikiContent'] = render_content(rec, page)
        if not rec['WikiContent']:
            rec['WikiContent'] = NO_PAGE_NOTE
            rec['IsStub'] = 1
        records.append(rec)
        return rec

    # --- 战斗异常 ---
    for it in idx['anomalies']:
        page = parsed.get(it['page']) or {}
        tier = int(re.sub(r'\D', '', it.get('tier', '0')) or 0)
        level = int(it.get('level') or 0)
        sec = security_bits(it.get('high'), it.get('low'), it.get('null'))
        esc = ''
        if it.get('escalation'):
            esc_name = PAGE_CN.get(it.get('escalationPage') or '', '') or name_cn(it['escalation'])
            esc = ('%s（%s）' % (esc_name, it['escalation'])) if esc_name != it['escalation'] else it['escalation']
        ship = (page.get('shipLimit') or '').strip().lower()
        rec = add(3, FACTION_EN.get(it['faction'], -1), 0, tier, level, sec,
                  ship_limit_cn(page.get('shipLimit', '')), it['site'], esc,
                  '', security_cn(sec), it['page'])
        rec['_escPage'] = it.get('escalationPage') or ''

    # --- 无评级复合体 ---
    for it in idx['unrated']:
        page = parsed.get(it['page'])
        sec = sec_from_page((page or {}).get('location', '')) or security_bits(
            it.get('high'), it.get('low'), it.get('null'))
        info = page.get('shipLimit', '') if page else ''
        rating = 9 if 'fleet staging point' in it['site'].lower() else 0
        add(1, FACTION_EN.get(it['faction'], -1), rating, 0, 0, sec,
            ship_limit_cn(info), it['site'], '',
            '', security_cn(sec), it['page'])

    # --- DED 评级 ---
    for it in idx['dedRated']:
        page = parsed.get(it['page'])
        sec = sec_from_page((page or {}).get('location', '')) or security_bits(
            it.get('high'), it.get('low'), it.get('null'))
        info = page.get('shipLimit', '') if page else ''
        rating = int(it['rating'].split('/')[0])
        add(0, FACTION_EN.get(it['faction'], -1), rating, 0, 0, sec,
            ship_limit_cn(info), it['site'], '',
            '', security_cn(sec), it['page'])

    # --- 远征 ---
    for it in idx['expeditions']:
        page = parsed.get(it['page'])
        fac = FACTION_EN.get(it['faction'], -1)
        add(2, fac, 0, 0, 0, 0, '', it['site'], '',
            source_cn(it.get('source', ''), fac), '', it['page'])

    # --- 化学实验室 ---
    for it in idx['chemicalLabs']:
        page = parsed.get(it['page'])
        loc = region_cn(it.get('location', ''))
        add(4, FACTION_EN.get(it['faction'], -1), 0, 0, 0, 0, '', it['site'], '',
            '', loc, it['page'])

    # --- 三神裔 ---
    for it in idx['triglavian']:
        page = parsed.get(it['page'])
        cls = it.get('cls', '')
        add(6, -1, 0, 0, 0, 0, '', it['site'], '', '',
            '波赫文（Pochven）· ' + (tr(cls) if cls else ''), it['page'])

    # --- 战斗遗迹 ---
    for it in idx.get('relicSites', []):
        page = parsed.get(it['page'])
        add(5, -1, 0, 0, 0, 4, 'T2 战列舰', it['site'], '', '',
            RELIC_REGION.get(it['site'], ''), it['page'])

    # --- 特殊 ---
    for it in idx.get('special', []):
        page = parsed.get(it['page'])
        add(7, -1, 0, 0, 0, 0, '', it['site'], '', '',
            '', it['page'])

    # --- 旗舰远征（数据来自 SDE dungeons.jsonl，wiki 没有这些条目） ---
    capitals = load_json('capital_sites.json', []) or []
    for cap in capitals:
        en = cap['en']
        rec = add(8, cap.get('faction', -1), 0, 0, 0, 0, '', en, '', '',
                  '', '')
        # 用官方中英文名覆盖（add 里按现有库/译文取的名字优先级最高，这里必须是官方名）
        rec['TitleEN'] = en
        rec['TitleCN'] = cap.get('zh') or en
        rec['Source'] = CAPITAL_SOURCE.get(en, '')
        rec['IsStub'] = 1
        rec['WikiTitle'] = ''
        rec['WikiUpdatedAt'] = ''
        rec['WikiContent'] = render_capital(cap)
    if capitals:
        print('  旗舰远征（SDE）：%d 条' % len(capitals))

    # 站点名兜底：把译文里的中文名写回 TitleCN（上面 add 已处理，这里只补空）
    for r in records:
        if not r['TitleCN']:
            r['TitleCN'] = r['TitleEN']

    # 官方名会撞车（同名不同站），统一加后缀区分
    renamed = disambiguate(records)
    official = sum(1 for r in records if official_name(r['TitleEN'], r['Faction']))
    print('  官方中文名（SDE）：命中 %d / %d 条；为消重命名 %d 条' % (official, len(records), len(renamed)))
    for old, new, en in renamed:
        print('    %-18s -> %-24s (%s)' % (old, new, en))

    # 升级/触发关系落成 Id：按 wiki 页名匹配（比按中文名匹配可靠——中文名会被 SDE 纠名改掉）
    by_page = {}
    for r in records:
        key = r.get('WikiTitle') or ''
        if key:
            by_page.setdefault(key, r['Id'])
    linked = 0
    for r in records:
        esc_page = r.pop('_escPage', '') if '_escPage' in r else ''
        if esc_page and esc_page in by_page:
            r['EscalationId'] = by_page[esc_page]
            linked += 1
    print('  升级目标已关联 Id：%d 条' % linked)

    # 旗舰远征反向链接：这 3 条异常在 wiki 上就把"可升级为"写成了旗舰远征（没有独立页面，所以上面按页名匹配不到）
    cap_by_en = {r['TitleEN'].strip(): r for r in records if r['Category'] == 8}
    cap_linked = 0
    for cap_en, anom_en in CAPITAL_SOURCE_SITE.items():
        cap_rec = cap_by_en.get(cap_en)
        if cap_rec is None:
            continue
        for r in records:
            if r['Category'] == 3 and r['TitleEN'].strip().lower() == anom_en.lower():
                r['EscalationId'] = cap_rec['Id']
                r['Escalation'] = '%s（旗舰）' % cap_rec['TitleCN']
                cap_linked += 1
    print('  旗舰远征反向链接：%d 条' % cap_linked)

    json.dump(records, io.open(os.path.join(HERE, 'sites.json'), 'w', encoding='utf-8'),
              ensure_ascii=False, indent=1)

    by_cat = {}
    for r in records:
        by_cat[r['Category']] = by_cat.get(r['Category'], 0) + 1
    print('生成 %d 条站点：' % len(records))
    for c in sorted(by_cat):
        print('  %-14s %3d' % (CATEGORY_CN[c], by_cat[c]))
    migrated = sum(1 for r in records if r['CommunityContent'])
    print('  其中带社区中文攻略：%d 条' % migrated)
    print('  带 wiki 中文正文：%d 条' % sum(1 for r in records if r['WikiContent']))
    print('  stub：%d 条' % sum(1 for r in records if r['IsStub']))
    return records


def write_db(records):
    if not os.path.exists(STATIC_DB):
        print('找不到 static.db：%s' % STATIC_DB)
        return
    con = sqlite3.connect(STATIC_DB)
    cur = con.cursor()
    cur.execute('DROP TABLE IF EXISTS CombatSite')
    cur.execute('''CREATE TABLE CombatSite (
        Id INTEGER NOT NULL PRIMARY KEY,
        Category INTEGER NOT NULL,
        Faction INTEGER NOT NULL,
        Rating INTEGER NOT NULL,
        Tier INTEGER NOT NULL,
        Level INTEGER NOT NULL,
        Security INTEGER NOT NULL,
        ShipLimit TEXT,
        TitleCN TEXT,
        TitleEN TEXT,
        Escalation TEXT,
        Source TEXT,
        Location TEXT,
        EscalationId INTEGER NOT NULL DEFAULT 0,
        WikiTitle TEXT,
        CommunityContent TEXT,
        WikiContent TEXT,
        IsStub INTEGER NOT NULL DEFAULT 0,
        CommunityUpdatedAt TEXT,
        WikiUpdatedAt TEXT
    )''')
    cur.executemany(
        'INSERT INTO CombatSite (Id, Category, Faction, Rating, Tier, Level, Security, ShipLimit,'
        ' TitleCN, TitleEN, Escalation, EscalationId, Source, Location, WikiTitle, CommunityContent, WikiContent,'
        ' IsStub, CommunityUpdatedAt, WikiUpdatedAt) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)',
        [(r['Id'], r['Category'], r['Faction'], r['Rating'], r['Tier'], r['Level'], r['Security'],
          r['ShipLimit'], r['TitleCN'], r['TitleEN'], r['Escalation'], r['EscalationId'], r['Source'], r['Location'],
          r['WikiTitle'], r['CommunityContent'], r['WikiContent'], r['IsStub'],
          r['CommunityUpdatedAt'], r['WikiUpdatedAt']) for r in records])
    cur.execute('CREATE INDEX IF NOT EXISTS IX_CombatSite_Category ON CombatSite(Category)')
    cur.execute('CREATE INDEX IF NOT EXISTS IX_CombatSite_Faction ON CombatSite(Faction)')
    con.commit()
    con.execute('VACUUM')
    con.commit()
    con.close()
    print('已写入 %s（%d 条）' % (STATIC_DB, len(records)))


def main():
    records = build()
    if '--write-db' in sys.argv:
        write_db(records)


if __name__ == '__main__':
    main()




