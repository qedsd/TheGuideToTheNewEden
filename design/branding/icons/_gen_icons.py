# -*- coding: utf-8 -*-
"""生成新伊甸漫游指南导航图标：单文件 sprite + 每个图标独立 svg + 预览页"""
import os, re

OUT = r"D:\QEDSD\Code\TheGuideToTheNewEden\design\branding\icons"
os.makedirs(OUT, exist_ok=True)

# (id, 中文名, Nav 资源键, 主色, svg 内容)
ICONS = [
# ---- 顶级导航 ----
("home", "主页", "Nav.Home", "#38BDF8", '''
<path d="M20 60 L64 22 L108 60" fill="none" stroke="#38BDF8" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M32 55 V100 H96 V55" fill="none" stroke="#38BDF8" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
<rect x="56" y="74" width="16" height="26" rx="5" fill="#5EEAD4"/>'''),

("characters", "角色", "Nav.Characters", "#8B5CF6", '''
<circle cx="64" cy="46" r="20" fill="none" stroke="#8B5CF6" stroke-width="9"/>
<path d="M26 106 C26 82 40 72 64 72 C88 72 102 82 102 106" fill="none" stroke="#8B5CF6" stroke-width="9" stroke-linecap="round"/>
<circle cx="64" cy="106" r="5" fill="#5EEAD4"/>'''),

("business", "商业", "Nav.Business", "#2563EB", '''
<path d="M26 88 L50 62 L70 74 L100 38" fill="none" stroke="#2563EB" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M72 38 H100 V66" fill="none" stroke="#22C55E" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>
<circle cx="50" cy="62" r="7" fill="#38BDF8"/>
<circle cx="70" cy="74" r="7" fill="#38BDF8"/>'''),

("gamePreview", "多开", "Nav.GamePreview", "#6366F1", '''
<rect x="14" y="24" width="44" height="36" rx="6" fill="none" stroke="#6366F1" stroke-width="8"/>
<rect x="14" y="66" width="44" height="36" rx="6" fill="none" stroke="#6366F1" stroke-width="8"/>
<rect x="70" y="24" width="44" height="78" rx="6" fill="none" stroke="#6366F1" stroke-width="8"/>
<circle cx="36" cy="42" r="6" fill="#5EEAD4"/>
<circle cx="36" cy="84" r="6" fill="#38BDF8"/>
<circle cx="92" cy="63" r="7" fill="#A78BFA"/>'''),

("assistant", "AI 助手", "Nav.Assistant", "#8B5CF6", '''
<rect x="30" y="30" width="68" height="68" rx="18" fill="none" stroke="#8B5CF6" stroke-width="9"/>
<path d="M64 44 Q64 64 84 64 Q64 64 64 84 Q64 64 44 64 Q64 64 64 44 Z" fill="#A78BFA"/>
<path d="M64 10 V26 M64 102 V118 M10 64 H26 M102 64 H118" stroke="#8B5CF6" stroke-width="8" stroke-linecap="round"/>'''),

("channel", "频道", "Nav.Channel", "#38BDF8", '''
<rect x="14" y="26" width="100" height="58" rx="16" fill="none" stroke="#38BDF8" stroke-width="9"/>
<path d="M40 82 L32 106 L64 82 Z" fill="#38BDF8"/>
<circle cx="46" cy="55" r="6" fill="#5EEAD4"/>
<circle cx="64" cy="55" r="6" fill="#5EEAD4"/>
<circle cx="82" cy="55" r="6" fill="#5EEAD4"/>'''),

("translation", "翻译", "Nav.Translation", "#6366F1", '''
<rect x="12" y="38" width="40" height="52" rx="10" fill="none" stroke="#6366F1" stroke-width="9"/>
<rect x="76" y="38" width="40" height="52" rx="10" fill="none" stroke="#2DD4BF" stroke-width="9"/>
<path d="M58 64 H70" stroke="#38BDF8" stroke-width="8" stroke-linecap="round"/>
<path d="M62 54 L72 64 L62 74" fill="none" stroke="#38BDF8" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'''),

("guides", "攻略", "Nav.Guides", "#22C55E", '''
<path d="M64 34 C48 24 28 24 18 30 V92 C28 86 48 86 64 96 Z" fill="none" stroke="#22C55E" stroke-width="9" stroke-linejoin="round"/>
<path d="M64 34 C80 24 100 24 110 30 V92 C100 86 80 86 64 96 Z" fill="none" stroke="#86EFAC" stroke-width="9" stroke-linejoin="round"/>
<path d="M64 34 V96" stroke="#F59E0B" stroke-width="8"/>'''),

("map", "星图", "Nav.Map", "#6366F1", '''
<path d="M26 96 L52 46 L90 72 L104 30" fill="none" stroke="#94A3B8" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M52 46 L104 30" fill="none" stroke="#94A3B8" stroke-width="6" stroke-linecap="round" opacity="0.6"/>
<circle cx="26" cy="96" r="11" fill="#38BDF8"/>
<circle cx="52" cy="46" r="13" fill="#6366F1"/>
<circle cx="90" cy="72" r="11" fill="#A78BFA"/>
<circle cx="104" cy="30" r="9" fill="#5EEAD4"/>'''),

("wormhole", "虫洞", "Nav.Wormhole", "#2DD4BF", '''
<g fill="none" stroke-width="8" transform="rotate(-28 64 64)">
<ellipse cx="64" cy="64" rx="46" ry="24" stroke="#2DD4BF"/>
<ellipse cx="64" cy="64" rx="34" ry="17" stroke="#818CF8"/>
<ellipse cx="64" cy="64" rx="22" ry="11" stroke="#A78BFA"/>
</g>
<circle cx="64" cy="64" r="10" fill="#5EEAD4"/>'''),

("links", "快速链接", "Nav.Links", "#38BDF8", '''
<rect x="16" y="46" width="52" height="36" rx="18" fill="none" stroke="#38BDF8" stroke-width="9" transform="rotate(-45 42 64)"/>
<rect x="60" y="46" width="52" height="36" rx="18" fill="none" stroke="#6366F1" stroke-width="9" transform="rotate(-45 86 64)"/>'''),

("zkb", "Zkillboard", "Nav.ZKB", "#EF4444", '''
<circle cx="64" cy="52" r="30" fill="none" stroke="#EF4444" stroke-width="7" opacity="0.5"/>
<path d="M64 12 L74 42 L104 52 L74 62 L64 92 L54 62 L24 52 L54 42 Z" fill="#EF4444"/>
<circle cx="64" cy="52" r="10" fill="#F59E0B"/>'''),

("database", "数据库", "Nav.Database", "#3B82F6", '''
<ellipse cx="64" cy="30" rx="38" ry="13" fill="#BFDBFE"/>
<path d="M26 30 V56 C26 63 43 69 64 69 C85 69 102 63 102 56 V30" fill="none" stroke="#3B82F6" stroke-width="9"/>
<path d="M26 56 V84 C26 91 43 97 64 97 C85 97 102 91 102 84 V56" fill="none" stroke="#3B82F6" stroke-width="9"/>
<ellipse cx="64" cy="30" rx="38" ry="13" fill="none" stroke="#3B82F6" stroke-width="9"/>'''),

("eveSettingsSync", "配置同步", "Nav.EveSettingsSync", "#2DD4BF", '''
<rect x="12" y="34" width="40" height="56" rx="8" fill="none" stroke="#2DD4BF" stroke-width="9"/>
<rect x="76" y="34" width="40" height="56" rx="8" fill="none" stroke="#6366F1" stroke-width="9"/>
<path d="M58 60 H70" stroke="#F59E0B" stroke-width="8" stroke-linecap="round"/>
<path d="M62 52 L72 60 L62 68" fill="none" stroke="#F59E0B" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M70 80 H58" stroke="#38BDF8" stroke-width="8" stroke-linecap="round"/>
<path d="M66 72 L56 80 L66 88" fill="none" stroke="#38BDF8" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'''),

("tools", "小工具", "Nav.Tools", "#64748B", '''
<rect x="16" y="46" width="96" height="60" rx="12" fill="none" stroke="#64748B" stroke-width="9"/>
<path d="M48 46 V30 A8 8 0 0 1 56 22 H72 A8 8 0 0 1 80 30 V46" fill="none" stroke="#64748B" stroke-width="9" stroke-linecap="round"/>
<circle cx="64" cy="76" r="10" fill="#F59E0B"/>'''),

("settings", "设置", "Nav.Settings", "#64748B", '''
<g fill="#64748B">
<rect x="57" y="26" width="14" height="14" rx="3"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(45 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(90 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(135 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(180 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(225 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(270 64 64)"/>
<rect x="57" y="26" width="14" height="14" rx="3" transform="rotate(315 64 64)"/>
</g>
<circle cx="64" cy="64" r="20" fill="none" stroke="#64748B" stroke-width="9"/>
<circle cx="64" cy="64" r="8" fill="#5EEAD4"/>'''),

# ---- 商业 ----
("market", "市场", "Nav.Market", "#22C55E", '''
<rect x="16" y="52" width="18" height="44" rx="5" fill="#22C55E"/>
<rect x="40" y="30" width="18" height="66" rx="5" fill="#4ADE80"/>
<rect x="70" y="34" width="18" height="62" rx="5" fill="#F87171"/>
<rect x="94" y="56" width="18" height="40" rx="5" fill="#FCA5A5"/>
<path d="M64 20 V108" stroke="#38BDF8" stroke-width="6" stroke-linecap="round"/>'''),

("order", "订单", "Nav.Order", "#3B82F6", '''
<rect x="24" y="18" width="64" height="86" rx="10" fill="none" stroke="#3B82F6" stroke-width="9"/>
<path d="M40 44 H72" stroke="#3B82F6" stroke-width="8" stroke-linecap="round"/>
<path d="M40 62 H60" stroke="#3B82F6" stroke-width="8" stroke-linecap="round"/>
<path d="M74 74 L82 82 L98 64" fill="none" stroke="#22C55E" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>'''),

("scalper", "倒货", "Nav.Scalper", "#38BDF8", '''
<rect x="14" y="66" width="24" height="26" rx="6" fill="#38BDF8"/>
<rect x="14" y="34" width="24" height="26" rx="6" fill="#60A5FA"/>
<path d="M48 76 H92" stroke="#F59E0B" stroke-width="9" stroke-linecap="round"/>
<path d="M74 58 L92 76 L74 94" fill="none" stroke="#F59E0B" stroke-width="9" stroke-linecap="round" stroke-linejoin="round"/>'''),

("compareMarket", "比价", "Nav.CompareMarket", "#8B5CF6", '''
<rect x="16" y="60" width="28" height="44" rx="6" fill="#38BDF8"/>
<rect x="84" y="28" width="28" height="76" rx="6" fill="#EC4899"/>
<path d="M54 74 H74" stroke="#8B5CF6" stroke-width="7" stroke-linecap="round"/>
<path d="M66 66 L74 74 L66 82" fill="none" stroke="#8B5CF6" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
<circle cx="30" cy="30" r="6" fill="#5EEAD4"/>'''),

("appraisal", "估价", "Nav.Appraisal", "#2DD4BF", '''
<rect x="18" y="52" width="50" height="46" rx="8" fill="none" stroke="#2DD4BF" stroke-width="9"/>
<path d="M18 68 H68" stroke="#2DD4BF" stroke-width="8"/>
<path d="M30 52 V42" stroke="#2DD4BF" stroke-width="8" stroke-linecap="round"/>
<circle cx="82" cy="44" r="22" fill="none" stroke="#38BDF8" stroke-width="9"/>
<path d="M98 60 L112 74" stroke="#38BDF8" stroke-width="10" stroke-linecap="round"/>'''),

# ---- 频道 ----
("channelIntel", "频道预警", "Nav.ChannelIntel", "#F43F5E", '''
<circle cx="64" cy="64" r="40" fill="none" stroke="#F43F5E" stroke-width="9"/>
<path d="M64 24 A40 40 0 0 1 104 64" fill="none" stroke="#5EEAD4" stroke-width="8" stroke-linecap="round"/>
<path d="M57 44 H71 V68 H57 Z" fill="#EF4444"/>
<circle cx="64" cy="86" r="7" fill="#EF4444"/>'''),

("channelMonitor", "频道监控", "Nav.ChannelMonitor", "#2DD4BF", '''
<path d="M12 64 C32 40 48 26 64 26 C80 26 96 40 116 64 C96 88 80 102 64 102 C48 102 32 88 12 64 Z" fill="none" stroke="#2DD4BF" stroke-width="9" stroke-linejoin="round"/>
<circle cx="64" cy="64" r="18" fill="#38BDF8"/>
<circle cx="64" cy="64" r="7" fill="#1E3A8A"/>'''),

("channelScan", "频道统计", "Nav.ChannelScan", "#6366F1", '''
<rect x="20" y="64" width="22" height="42" rx="6" fill="#6366F1"/>
<rect x="53" y="42" width="22" height="64" rx="6" fill="#818CF8"/>
<rect x="86" y="24" width="22" height="82" rx="6" fill="#5EEAD4"/>'''),

("channelMarket", "频道查价", "Nav.ChannelMarket", "#22C55E", '''
<rect x="16" y="22" width="96" height="56" rx="16" fill="none" stroke="#22C55E" stroke-width="9"/>
<path d="M46 76 L40 100 L68 76 Z" fill="#22C55E"/>
<path d="M36 44 L54 60 L72 36 L94 54" fill="none" stroke="#22C55E" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>
<circle cx="72" cy="36" r="7" fill="#5EEAD4"/>'''),

("channelTranslation", "频道翻译", "Nav.ChannelTranslation", "#38BDF8", '''
<rect x="12" y="24" width="94" height="54" rx="16" fill="none" stroke="#38BDF8" stroke-width="9"/>
<path d="M38 76 L32 100 L60 76 Z" fill="#38BDF8"/>
<path d="M34 44 H58" stroke="#5EEAD4" stroke-width="8" stroke-linecap="round"/>
<path d="M34 60 H48" stroke="#5EEAD4" stroke-width="8" stroke-linecap="round"/>
<path d="M74 44 H96" stroke="#A78BFA" stroke-width="8" stroke-linecap="round"/>
<path d="M74 60 H96" stroke="#A78BFA" stroke-width="8" stroke-linecap="round"/>'''),

("gameLogMonitor", "日志监控", "Nav.GameLogMonitor", "#64748B", '''
<rect x="24" y="14" width="70" height="86" rx="10" fill="none" stroke="#64748B" stroke-width="9"/>
<path d="M42 40 H74" stroke="#64748B" stroke-width="8" stroke-linecap="round"/>
<path d="M42 58 H64" stroke="#64748B" stroke-width="8" stroke-linecap="round"/>
<path d="M34 78 L46 66 L58 78 L70 66 L82 78" fill="none" stroke="#2DD4BF" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'''),

# ---- 翻译 ----
("translationAi", "AI 翻译", "Nav.TranslationAi", "#8B5CF6", '''
<rect x="18" y="24" width="62" height="54" rx="16" fill="none" stroke="#8B5CF6" stroke-width="9"/>
<path d="M40 76 L34 100 L62 76 Z" fill="#8B5CF6"/>
<path d="M96 40 Q96 56 112 56 Q96 56 96 72 Q96 56 80 56 Q96 56 96 40 Z" fill="#5EEAD4"/>'''),

("translationLocal", "本地词库", "Nav.TranslationLocal", "#0EA5E9", '''
<path d="M64 36 C50 24 30 24 18 30 V92 C30 86 50 86 64 98 Z" fill="none" stroke="#0EA5E9" stroke-width="9" stroke-linejoin="round"/>
<path d="M64 36 C78 24 98 24 110 30 V92 C98 86 78 86 64 98 Z" fill="none" stroke="#2DD4BF" stroke-width="9" stroke-linejoin="round"/>
<path d="M64 36 V98" stroke="#6366F1" stroke-width="7"/>'''),

# ---- 攻略 ----
("ded", "死亡远征", "Nav.DED", "#F43F5E", '''
<path d="M32 56 C32 34 46 22 66 22 C86 22 100 34 100 56 V70 C100 80 92 86 82 86 H50 C40 86 32 80 32 70 Z" fill="none" stroke="#F43F5E" stroke-width="9" stroke-linejoin="round"/>
<circle cx="54" cy="58" r="8" fill="#F43F5E"/>
<circle cx="78" cy="58" r="8" fill="#F43F5E"/>
<path d="M52 86 V102 M64 86 V102 M76 86 V102" stroke="#F43F5E" stroke-width="8" stroke-linecap="round"/>'''),

("mission", "任务攻略", "Nav.Mission", "#F59E0B", '''
<path d="M38 16 V112" stroke="#F59E0B" stroke-width="9" stroke-linecap="round"/>
<path d="M38 26 C60 18 74 30 94 22 V56 C74 64 60 52 38 60 Z" fill="#F59E0B"/>
<circle cx="38" cy="112" r="6" fill="#5EEAD4"/>'''),

# ---- 小工具 ----
("toolsIME", "快捷输入", "Nav.Tools_IME", "#64748B", '''
<rect x="10" y="42" width="108" height="58" rx="12" fill="none" stroke="#64748B" stroke-width="9"/>
<circle cx="30" cy="60" r="5" fill="#38BDF8"/>
<circle cx="48" cy="60" r="5" fill="#38BDF8"/>
<circle cx="66" cy="60" r="5" fill="#38BDF8"/>
<circle cx="84" cy="60" r="5" fill="#38BDF8"/>
<circle cx="102" cy="60" r="5" fill="#38BDF8"/>
<rect x="46" y="76" width="36" height="10" rx="5" fill="#94A3B8"/>
<path d="M56 20 L64 30 L72 20" fill="none" stroke="#5EEAD4" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'''),

("serverPing", "游戏延迟", "Nav.Tools_ServerPing", "#38BDF8", '''
<path d="M26 78 A44 44 0 0 1 102 78" fill="none" stroke="#38BDF8" stroke-width="9" stroke-linecap="round"/>
<path d="M40 60 A32 32 0 0 1 88 60" fill="none" stroke="#5EEAD4" stroke-width="9" stroke-linecap="round"/>
<path d="M52 42 A18 18 0 0 1 76 42" fill="none" stroke="#6366F1" stroke-width="9" stroke-linecap="round"/>
<circle cx="64" cy="98" r="10" fill="#38BDF8"/>'''),

("keyboardSpan", "键盘", "Nav.Tools_KeyboardSpan", "#6366F1", '''
<rect x="10" y="42" width="108" height="58" rx="12" fill="none" stroke="#6366F1" stroke-width="9"/>
<rect x="44" y="54" width="40" height="14" rx="6" fill="#A78BFA"/>
<rect x="44" y="76" width="40" height="12" rx="6" fill="#C4B5FD"/>
<path d="M56 22 L64 32 L72 22" fill="none" stroke="#F59E0B" stroke-width="8" stroke-linecap="round" stroke-linejoin="round"/>'''),

("taskbar", "分屏", "Nav.Tools_Taskbar", "#0EA5E9", '''
<rect x="12" y="24" width="104" height="80" rx="12" fill="none" stroke="#0EA5E9" stroke-width="9"/>
<path d="M64 24 V104" stroke="#0EA5E9" stroke-width="8"/>
<rect x="22" y="34" width="34" height="22" rx="5" fill="#38BDF8"/>
<rect x="72" y="34" width="34" height="22" rx="5" fill="#5EEAD4"/>
<path d="M22 74 H44 M72 74 H94" stroke="#94A3B8" stroke-width="8" stroke-linecap="round"/>'''),
]

HEAD = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128">'

# 1) 单文件
for cid, name, key, color, body in ICONS:
    body = body.strip()
    with open(os.path.join(OUT, f"nav_{cid}.svg"), "w", encoding="utf-8") as f:
        f.write(f'{HEAD}\n  <!-- {name} · {key} -->\n  {body}\n</svg>\n')

# 2) sprite
syms = "\n".join(
    f'  <symbol id="{cid}" viewBox="0 0 128 128">\n    {body.strip()}\n  </symbol>'
    for cid, name, key, color, body in ICONS)
with open(os.path.join(OUT, "nav-icons.svg"), "w", encoding="utf-8") as f:
    f.write(f'{HEAD}\n  <!-- 新伊甸漫游指南 导航图标 sprite（{len(ICONS)} 个） -->\n{syms}\n</svg>\n')

# 3) 预览页
cells = "\n".join(
    f'''      <div class="cell">
        <svg class="ic" viewBox="0 0 128 128"><use href="#{cid}"/></svg>
        <div class="nm">{name}</div>
        <div class="kb">{key}</div>
      </div>''' for cid, name, key, color, body in ICONS)

sprite_inline = "\n".join(
    f'    <symbol id="s-{cid}" viewBox="0 0 128 128">\n      {body.strip()}\n    </symbol>'
    for cid, name, key, color, body in ICONS)

ladders = "\n".join(
    f'''      <div class="cell">
        <svg class="ic" viewBox="0 0 128 128"><use href="#s-{cid}"/></svg>
        <div class="nm">{name}</div>
        <div class="kb">{key}</div>
      </div>''' for cid, name, key, color, body in ICONS)

html = f'''<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>新伊甸漫游指南 · 导航图标（{len(ICONS)} 个）</title>
<style>
  :root {{ --bg:#F3F6FB; --panel:#FFFFFF; --text:#1B2540; --text2:#5A6B8C; --text3:#8593AE; --stroke:#DDE5F0; --card:#EDF2FA; --shadow:0 2px 10px rgba(27,37,64,.08); }}
  html[data-theme="dark"] {{ --bg:#0D1220; --panel:#141B2E; --text:#EDF1FA; --text2:#9DA9C4; --text3:#6B7791; --stroke:#263050; --card:#1A2238; --shadow:0 2px 12px rgba(0,0,0,.45); }}
  * {{ box-sizing:border-box; margin:0; padding:0; }}
  body {{ font-family:"Segoe UI","Microsoft YaHei","PingFang SC",sans-serif; background:var(--bg); color:var(--text); padding:32px 36px 60px; transition:background .25s,color .25s; }}
  .head {{ display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:14px; margin-bottom:6px; }}
  h1 {{ font-size:22px; }}
  .sub {{ color:var(--text2); font-size:13px; margin-top:6px; }}
  .toggle {{ border:1px solid var(--stroke); background:var(--panel); color:var(--text); padding:8px 16px; border-radius:8px; cursor:pointer; font-size:13px; box-shadow:var(--shadow); }}
  h2 {{ font-size:16px; margin:32px 0 6px; }}
  .desc {{ color:var(--text2); font-size:13px; margin-bottom:14px; line-height:1.7; }}
  .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(112px,1fr)); gap:10px; }}
  .cell {{ background:var(--panel); border:1px solid var(--stroke); border-radius:10px; padding:16px 8px 12px; text-align:center; box-shadow:var(--shadow); }}
  .cell .ic {{ width:26px; height:26px; display:block; margin:0 auto 10px; }}
  .cell .nm {{ font-size:13px; }}
  .cell .kb {{ font-size:11px; color:var(--text3); margin-top:3px; }}
  .sizes {{ display:flex; gap:30px; align-items:flex-end; padding:20px; justify-content:center; flex-wrap:wrap; }}
  .sizes .item {{ text-align:center; }}
  .sizes svg {{ display:block; margin:0 auto 8px; }}
  .sizes .cap {{ font-size:11px; color:var(--text3); }}
  .note {{ background:var(--card); border-left:3px solid #0EA5E9; padding:12px 16px; border-radius:0 8px 8px 0; font-size:12.5px; color:var(--text2); line-height:1.9; }}
  .note code {{ font-family:Consolas,monospace; background:var(--panel); padding:1px 6px; border-radius:4px; }}
</style>
</head>
<body>
<svg width="0" height="0" style="position:absolute"><defs>
{sprite_inline}
</defs></svg>

<div class="head">
  <div>
    <h1>导航图标 · {len(ICONS)} 个</h1>
    <div class="sub">128 画布 · 扁平 + 线性混合 · 主色描边 + 强调色实心细节 · 已在 20/24/32px 校验可读性</div>
  </div>
  <button class="toggle" onclick="document.documentElement.dataset.theme = document.documentElement.dataset.theme==='dark' ? 'light' : 'dark'">切换亮 / 暗</button>
</div>

<h2>全量图标</h2>
<p class="desc">每个格下方标注对应的本地化资源键，可直接对照 MainWindow.xaml 的 <code>Content={{DynamicResource ...}}</code> 替换。</p>
<div class="grid">
{cells}
</div>

<h2>尺寸校验</h2>
<p class="desc">以「星图」「虫洞」「市场」三个结构较复杂的图标为例。</p>
<div class="panel" style="background:var(--panel);border:1px solid var(--stroke);border-radius:10px;box-shadow:var(--shadow)">
  <div class="sizes">
    <div class="item"><svg viewBox="0 0 128 128" width="20" height="20"><use href="#s-map"/></svg><div class="cap">20px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="24" height="24"><use href="#s-map"/></svg><div class="cap">24px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="32" height="32"><use href="#s-map"/></svg><div class="cap">32px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="20" height="20"><use href="#s-wormhole"/></svg><div class="cap">20px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="24" height="24"><use href="#s-wormhole"/></svg><div class="cap">24px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="32" height="32"><use href="#s-wormhole"/></svg><div class="cap">32px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="20" height="20"><use href="#s-market"/></svg><div class="cap">20px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="24" height="24"><use href="#s-market"/></svg><div class="cap">24px</div></div>
    <div class="item"><svg viewBox="0 0 128 128" width="32" height="32"><use href="#s-market"/></svg><div class="cap">32px</div></div>
  </div>
</div>

<h2>文件与接入</h2>
<div class="note">
  <code>nav-icons.svg</code> — 全量 sprite（一个文件含 {len(ICONS)} 个 symbol，适合走 DrawingImage / Path 资源合并）<br>
  <code>nav_*.svg</code> — 每个图标独立文件（{len(ICONS)} 个，便于单独替换、按需引入）<br>
  <b>尚未改动任何工程代码。</b>后续接入方式（待确认再动）：把 NavigationViewItem 的 SymbolIcon 换成上述矢量，
  或先作为 ResourceDictionary 里的 DrawingImage 引入验证，再批量替换 Icon 属性。
</div>
</body>
</html>
'''
with open(os.path.join(OUT, "preview.html"), "w", encoding="utf-8") as f:
    f.write(html)

print(f"generated {len(ICONS)} icons -> {OUT}")
